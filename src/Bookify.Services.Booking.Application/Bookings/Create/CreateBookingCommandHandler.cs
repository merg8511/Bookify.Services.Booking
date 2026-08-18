using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.Services;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Bookings.Create;

public sealed class CreateBookingCommandHandler : ICommandHandler<CreateBookingCommand, Guid>
{
    private readonly IPropertyRepository _propertyRepository;
    private readonly IRentableUnitRepository _rentableUnitRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IBookingAvailabilityReader _bookingAvailabilityReader;
    private readonly IBookingInventoryLock _bookingInventoryLock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITransactionManager _transactionManager;

    public CreateBookingCommandHandler(
        IPropertyRepository propertyRepository,
        IRentableUnitRepository rentableUnitRepository,
        IBookingRepository bookingRepository,
        IBookingAvailabilityReader bookingAvailabilityReader,
        IBookingInventoryLock bookingInventoryLock,
        IUnitOfWork unitOfWork,
        ITransactionManager transactionManager)
    {
        _propertyRepository = propertyRepository
            ?? throw new ArgumentNullException(nameof(propertyRepository));

        _rentableUnitRepository = rentableUnitRepository
            ?? throw new ArgumentNullException(nameof(rentableUnitRepository));

        _bookingRepository = bookingRepository
            ?? throw new ArgumentNullException(nameof(bookingRepository));

        _bookingAvailabilityReader = bookingAvailabilityReader
            ?? throw new ArgumentNullException(nameof(bookingAvailabilityReader));

        _bookingInventoryLock = bookingInventoryLock
            ?? throw new ArgumentNullException(nameof(bookingInventoryLock));

        _unitOfWork = unitOfWork
            ?? throw new ArgumentNullException(nameof(unitOfWork));

        _transactionManager = transactionManager ??
            throw new ArgumentNullException(nameof(transactionManager));
    }

    public async Task<Result<Guid>> HandleAsync(
        CreateBookingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<StayPeriod> stayPeriodResult =
            StayPeriod.Create(
                command.CheckInDate!.Value,
                command.CheckOutDate!.Value);

        if (stayPeriodResult.IsFailure)
        {
            return Result<Guid>.Failure(
                stayPeriodResult.Error);
        }

        Result<GuestCount> guestCountResult =
            GuestCount.Create(
                command.GuestCount!.Value);

        if (guestCountResult.IsFailure)
        {
            return Result<Guid>.Failure(
                guestCountResult.Error);
        }

        StayPeriod stayPeriod = stayPeriodResult.Value;
        GuestCount guestCount = guestCountResult.Value;

        await using ITransaction transaction =
            await _transactionManager.BeginAsync(cancellationToken);

        try
        {
            bool propertyLocked =
                await _bookingInventoryLock
                    .TryAcquireAsync(
                    command.PropertyId,
                    cancellationToken);

            if (!propertyLocked)
            {
                return await RollbackFailureAsync(
                    transaction,
                    CreateBookingErrors
                        .PropertyNotFound(command.PropertyId),
                    cancellationToken);
            }

            Property? property =
                await _propertyRepository
                    .GetByIdAsync(
                        command.PropertyId,
                        cancellationToken);

            if (property is null)
            {
                return await RollbackFailureAsync(
                    transaction,
                    CreateBookingErrors
                        .PropertyNotFound(
                            command.PropertyId),
                    cancellationToken);
            }

            if (!property.IsActive)
            {
                return await RollbackFailureAsync(
                    transaction,
                    CreateBookingErrors
                        .PropertyInactive(command.PropertyId),
                    cancellationToken);
            }

            RentableUnit? rentableUnit =
                await _rentableUnitRepository
                    .GetByIdAsync(
                        command.RentableUnitId,
                        cancellationToken);

            if (rentableUnit is null)
            {
                return await RollbackFailureAsync(
                    transaction,
                    CreateBookingErrors
                        .RentableUnitNotFound(command.RentableUnitId),
                    cancellationToken);
            }

            if (rentableUnit.PropertyId != property.Id)
            {
                return await RollbackFailureAsync(
                    transaction,
                    CreateBookingErrors
                        .RentableUnitPropertyMismatch(
                            rentableUnit.Id,
                            property.Id),
                    cancellationToken);
            }

            RentableUnitPricing? pricing = rentableUnit.Pricing;

            if (pricing is null)
            {
                return await RollbackFailureAsync(
                    transaction,
                    CreateBookingErrors
                        .PricingNotConfigured(rentableUnit.Id),
                    cancellationToken);
            }

            Result<PriceBreakdown> priceResult =
                BookingPricingEngine.CalculatePrice(
                    pricing.RegularNightlyRate,
                    pricing.WeekendNightlyRate,
                    pricing.ExtraGuestNightlyRate,
                    rentableUnit,
                    guestCount,
                    stayPeriod,
                    rentableUnit.PricingSeasons);

            if(priceResult.IsFailure)
            {
                return await RollbackFailureAsync(
                    transaction,
                    priceResult.Error,
                    cancellationToken);
            }

            PriceSnapshot priceSnapshot =
                    PriceSnapshot.Create(priceResult.Value);

            Result<DomainBooking> bookingResult =
                DomainBooking.Create(
                    rentableUnit,
                    stayPeriod,
                    guestCount,
                    priceSnapshot);

            if (bookingResult.IsFailure)
            {
                return await RollbackFailureAsync(
                    transaction,
                    bookingResult.Error,
                    cancellationToken);
            }

            bool hasConflict =
                await _bookingAvailabilityReader
                    .HasConflictAsync(
                        property.Id,
                        rentableUnit.Id,
                        stayPeriodResult.Value.CheckInDate,
                        stayPeriodResult.Value.CheckOutDate,
                        cancellationToken);

            if (hasConflict)
            {
                return await RollbackFailureAsync(
                    transaction,
                    CreateBookingErrors
                        .NotAvailable,
                    cancellationToken);
            }

            DomainBooking booking = bookingResult.Value;

            _bookingRepository.Add(booking);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Result<Guid>.Success(booking.Id);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<Result<Guid>>
        RollbackFailureAsync(
            ITransaction transaction,
            Error error,
            CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);

        return Result<Guid>.Failure(error);
    }
}
