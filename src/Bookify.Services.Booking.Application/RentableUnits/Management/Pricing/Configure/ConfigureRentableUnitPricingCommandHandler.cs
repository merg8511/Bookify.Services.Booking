using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Pricing.Configure;

public sealed class ConfigureRentableUnitPricingCommandHandler :
    ICommandHandler<ConfigureRentableUnitPricingCommand>
{
    private const decimal MaxSupportedAmount = 999999999999999.999m;
    private readonly IPropertyRepository _propertyRepository;
    private readonly IRentableUnitRepository _unitRepository;
    private readonly IRentableUnitPricingReadService _pricingReadService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITransactionManager _transactionManager;
    private readonly IBookingInventoryLock _inventoryLock;

    public ConfigureRentableUnitPricingCommandHandler(
        IPropertyRepository propertyRepository,
        IRentableUnitRepository unitRepository,
        IRentableUnitPricingReadService pricingReadService,
        IUnitOfWork unitOfWork,
        ITransactionManager transactionManager,
        IBookingInventoryLock inventoryLock)
    {
        _propertyRepository = propertyRepository ??
            throw new ArgumentNullException(nameof(propertyRepository));

        _unitRepository = unitRepository ??
            throw new ArgumentNullException(nameof(unitRepository));

        _pricingReadService = pricingReadService ??
            throw new ArgumentNullException(nameof(pricingReadService));

        _unitOfWork = unitOfWork ??
            throw new ArgumentNullException(nameof(unitOfWork));

        _transactionManager = transactionManager ??
            throw new ArgumentNullException(nameof(transactionManager));

        _inventoryLock = inventoryLock ??
            throw new ArgumentNullException(nameof(inventoryLock));
    }

    public async Task<Result> HandleAsync(
        ConfigureRentableUnitPricingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<Money> regularResult = Money.Create(command.RegularNightlyRate, command.Currency);

        if (regularResult.IsFailure)
        {
            return Result.Failure(regularResult.Error);
        }

        Result<Money> weekendResult = Money.Create(command.WeekendNightlyRate, command.Currency);

        if (weekendResult.IsFailure)
        {
            return Result.Failure(weekendResult.Error);
        }

        Result<Money> extraGuestResult = Money.Create(command.ExtraGuestNightlyRate, command.Currency);

        if (extraGuestResult.IsFailure)
        {
            return Result.Failure(extraGuestResult.Error);
        }

        if (regularResult.Value.Amount > MaxSupportedAmount ||
           weekendResult.Value.Amount > MaxSupportedAmount ||
           extraGuestResult.Value.Amount > MaxSupportedAmount)
        {
            return Result.Failure(RentableUnitPricingManagementErrors.AmountOutOfRange);
        }

        Result<RentableUnitPricing> pricingResult = RentableUnitPricing.Create(
            regularResult.Value,
            weekendResult.Value,
            extraGuestResult.Value);

        if (pricingResult.IsFailure)
        {
            return Result.Failure(pricingResult.Error);
        }

        await using ITransaction transaction = await _transactionManager.BeginAsync(cancellationToken);

        bool propertyLocked = await _inventoryLock.TryAcquireAsync(command.PropertyId, cancellationToken);

        if (!propertyLocked)
        {
            return Result.Failure(RentableUnitManagementErrors.PropertyNotFound(command.PropertyId));
        }

        Property? property = await _propertyRepository.GetByIdAsync(command.PropertyId, cancellationToken);

        if (property is null)
        {
            return Result.Failure(RentableUnitManagementErrors.PropertyNotFound(command.PropertyId));
        }

        RentableUnit? unit = await _unitRepository.GetByIdAsync(command.UnitId, cancellationToken);

        if (unit is null || unit.PropertyId != command.PropertyId)
        {
            return Result.Failure(RentableUnitManagementErrors.UnitNotFound(command.UnitId));
        }

        bool hasIncompatibleSeasonCurrency = await _pricingReadService
            .HasIncompatibleSeasonCurrencyAsync(unit.Id, regularResult.Value.Currency, cancellationToken);

        if (hasIncompatibleSeasonCurrency)
        {
            return Result.Failure(RentableUnitPricingManagementErrors.SeasonCurrencyMismatch);
        }

        unit.ConfigurePricing(pricingResult.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
