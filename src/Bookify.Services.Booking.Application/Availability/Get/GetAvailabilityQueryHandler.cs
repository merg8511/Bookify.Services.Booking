using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Availability.ReadModels;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Application.Properties.ReadModels;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.Services;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Application.Availability.Get;

public sealed class GetAvailabilityQueryHandler : IQueryHandler<GetAvailabilityQuery, AvailabilityReadModel>
{

    private readonly IPropertyReadService _propertyReadService;
    private readonly IAvailabilityReadService _availabilityReadService;
    public GetAvailabilityQueryHandler(
        IPropertyReadService propertyReadService,
        IAvailabilityReadService availabilityReadService)
    {
        _availabilityReadService = availabilityReadService
            ?? throw new ArgumentNullException(nameof(availabilityReadService));
        _propertyReadService = propertyReadService
            ?? throw new ArgumentNullException(nameof(propertyReadService));
    }

    public async Task<Result<AvailabilityReadModel>> HandleAsync(
        GetAvailabilityQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        PropertyDetailsReadModel? property = await _propertyReadService.GetByIdAsync(query.PropertyId, cancellationToken);

        if (property is null)
        {
            return Result<AvailabilityReadModel>.Failure(
                GetAvailabilityErrors.PropertyNotFound(query.PropertyId));
        }

        if (!property.IsActive)
        {
            return Result<AvailabilityReadModel>.Failure(
                GetAvailabilityErrors.PropertyInactive(property.Id));
        }

        if (query.CheckInDate is null ||
            query.CheckOutDate is null ||
            query.GuestCount is null)
        {
            throw new InvalidOperationException(
                "Availability query must be validated before handling.");
        }

        Result<StayPeriod> stayPeriodResult = StayPeriod.Create(
            query.CheckInDate.Value,
            query.CheckOutDate.Value);

        if (stayPeriodResult.IsFailure)
        {
            return Result<AvailabilityReadModel>.Failure(stayPeriodResult.Error);
        }

        Result<GuestCount> guestCountResult = GuestCount.Create(query.GuestCount.Value);

        if (guestCountResult.IsFailure)
        {
            return Result<AvailabilityReadModel>.Failure(guestCountResult.Error);
        }

        StayPeriod stayPeriod = stayPeriodResult.Value;
        GuestCount guestCount = guestCountResult.Value;

        IReadOnlyList<AvailableRentableUnitReadModel> availableUnits = await _availabilityReadService
            .GetAvailableUnitsAsync(
                query.PropertyId,
                stayPeriod.CheckInDate,
                stayPeriod.CheckOutDate,
                guestCount.Value,
                cancellationToken);

        IReadOnlyList<AvailabilityPricingSeasonReadModel> pricingSeasons;

        if (availableUnits.Count == 0)
        {
            pricingSeasons = Array.Empty<AvailabilityPricingSeasonReadModel>();
        }
        else
        {
            Guid[] rentableUnitIds = availableUnits
                .Select(unit => unit.Id)
                .ToArray();

            pricingSeasons = await _availabilityReadService
                .GetPricingSeasonsAsync(
                    rentableUnitIds,
                    stayPeriod.CheckInDate,
                    stayPeriod.CheckOutDate,
                    cancellationToken);
        }

        Dictionary<Guid, AvailabilityPricingSeasonReadModel[]> seasonsByUnit = pricingSeasons
            .GroupBy(season => season.RentableUnitId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var quotedUnits = new List<AvailableRentableUnitReadModel>(availableUnits.Count);

        foreach (AvailableRentableUnitReadModel unit in availableUnits)
        {
            seasonsByUnit.TryGetValue(unit.Id, out AvailabilityPricingSeasonReadModel[]? unitSeasonRows);

            Result<AvailabilityQuoteReadModel> quoteResult = CalculateQuote(
                unit,
                unitSeasonRows ?? Array.Empty<AvailabilityPricingSeasonReadModel>(),
                guestCount,
                stayPeriod);

            if (quoteResult.IsFailure)
            {
                return Result<AvailabilityReadModel>.Failure(quoteResult.Error);
            }

            quotedUnits.Add(
                new AvailableRentableUnitReadModel
                {
                    Id = unit.Id,
                    PropertyId = unit.PropertyId,
                    Name = unit.Name,
                    Type = unit.Type,
                    MaximumCapacity = unit.MaximumCapacity,
                    MaxBaseGuests = unit.MaxBaseGuests,
                    IsEntireProperty = unit.IsEntireProperty,
                    RegularNightlyRateAmount = unit.RegularNightlyRateAmount,
                    WeekendNightlyRateAmount = unit.WeekendNightlyRateAmount,
                    ExtraGuestNightlyRateAmount = unit.ExtraGuestNightlyRateAmount,
                    PricingCurrency = unit.PricingCurrency,
                    Quote = quoteResult.Value
                });
        }

        var response = new AvailabilityReadModel(
            property.Id,
            property.Name,
            stayPeriod.CheckInDate,
            stayPeriod.CheckOutDate,
            stayPeriod.NumberOfNights,
            guestCount.Value,
            quotedUnits);

        return Result<AvailabilityReadModel>.Success(response);
    }

    public static Result<AvailabilityQuoteReadModel> CalculateQuote(
        AvailableRentableUnitReadModel unit,
        IReadOnlyCollection<AvailabilityPricingSeasonReadModel> seasonRows,
        GuestCount guestCount,
        StayPeriod stayPeriod)
    {
        Result<Money> regularRateResult = Money.Create(
            unit.RegularNightlyRateAmount,
            unit.PricingCurrency);

        if (regularRateResult.IsFailure)
        {
            return Result<AvailabilityQuoteReadModel>.Failure(regularRateResult.Error);
        }

        Result<Money> weekendRateResult = Money.Create(
            unit.WeekendNightlyRateAmount,
            unit.PricingCurrency);

        if (weekendRateResult.IsFailure)
        {
            return Result<AvailabilityQuoteReadModel>.Failure(weekendRateResult.Error);
        }

        Result<Money> extraGuestRateResult = Money.Create(
            unit.ExtraGuestNightlyRateAmount,
            unit.PricingCurrency);

        if (extraGuestRateResult.IsFailure)
        {
            return Result<AvailabilityQuoteReadModel>.Failure(extraGuestRateResult.Error);
        }

        var seasons = new List<PricingSeason>(seasonRows.Count);

        foreach (AvailabilityPricingSeasonReadModel seasonRow in seasonRows)
        {
            Result<Money> seasonRateResult = Money.Create(seasonRow.NightlyRateAmount, seasonRow.Currency);

            if (seasonRateResult.IsFailure)
            {
                return Result<AvailabilityQuoteReadModel>.Failure(seasonRateResult.Error);
            }

            Result<PricingSeason> seasonResult = PricingSeason.Create(
                seasonRow.StartDate,
                seasonRow.EndDate,
                seasonRateResult.Value,
                seasonRow.Priority);

            if (seasonResult.IsFailure)
            {
                return Result<AvailabilityQuoteReadModel>.Failure(seasonResult.Error);
            }

            seasons.Add(seasonResult.Value);
        }

        Result<PriceBreakdown> priceResult = BookingPricingEngine.CalculatePrice(
            regularRateResult.Value,
            weekendRateResult.Value,
            extraGuestRateResult.Value,
            unit.MaxBaseGuests,
            guestCount,
            stayPeriod,
            seasons);

        if (priceResult.IsFailure)
        {
            return Result<AvailabilityQuoteReadModel>.Failure(priceResult.Error);
        }

        PriceBreakdown breakdown = priceResult.Value;

        return Result<AvailabilityQuoteReadModel>.Success(
            new AvailabilityQuoteReadModel(
                breakdown.AccommodationPrice.Amount,
                breakdown.ExtraGuestPrice.Amount,
                breakdown.TotalPrice.Amount,
                breakdown.TotalPrice.Currency));
    }
}
