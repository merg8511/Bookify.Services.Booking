using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
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
    private readonly IUnitOfWork _unitOfWork;

    public CreateBookingCommandHandler(
        IPropertyRepository propertyRepository,
        IRentableUnitRepository rentableUnitRepository,
        IBookingRepository bookingRepository,
        IBookingAvailabilityReader bookingAvailabilityReader,
        IUnitOfWork unitOfWork)
    {
        _propertyRepository = propertyRepository
            ?? throw new ArgumentNullException(nameof(propertyRepository));

        _rentableUnitRepository = rentableUnitRepository
            ?? throw new ArgumentNullException(nameof(rentableUnitRepository));

        _bookingRepository = bookingRepository
            ?? throw new ArgumentNullException(nameof(bookingRepository));

        _bookingAvailabilityReader = bookingAvailabilityReader
            ?? throw new ArgumentNullException(nameof(bookingAvailabilityReader));

        _unitOfWork = unitOfWork
            ?? throw new ArgumentNullException(nameof(unitOfWork));
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

        Property? property = await _propertyRepository
                                .GetByIdAsync(
                                    command.PropertyId,
                                    cancellationToken);

        if (property is null)
        {
            return Result<Guid>.Failure(
                CreateBookingErrors
                    .PropertyNotFound(command.PropertyId));
        }

        if (!property.IsActive)
        {
            return Result<Guid>.Failure(
                CreateBookingErrors
                    .PropertyInactive(command.PropertyId));
        }

        RentableUnit? rentableUnit =
            await _rentableUnitRepository
                .GetByIdAsync(
                    command.RentableUnitId,
                    cancellationToken);

        if (rentableUnit is null)
        {
            return Result<Guid>.Failure(
                CreateBookingErrors
                    .RentableUnitNotFound(command.RentableUnitId));
        }

        if (rentableUnit.PropertyId != property.Id)
        {
            return Result<Guid>.Failure(
                CreateBookingErrors
                    .RentableUnitPropertyMismatch(
                        rentableUnit.Id,
                        property.Id));
        }

        Result<DomainBooking> bookingResult =
            DomainBooking.Create(
                rentableUnit,
                stayPeriodResult.Value,
                guestCountResult.Value);

        if (bookingResult.IsFailure)
        {
            return Result<Guid>.Failure(
                bookingResult.Error);
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
            return Result<Guid>.Failure(
                CreateBookingErrors
                    .NotAvailable);
        }

        DomainBooking booking = bookingResult.Value;

        _bookingRepository.Add(booking);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(booking.Id);
    }
}
