using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Availability.ReadModels;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Application.Properties.ReadModels;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.Services;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Application.Availability.Get;

public sealed class GetAvailabilityQueryHandler :
    IQueryHandler<GetAvailabilityQuery, AvailabilityReadModel>
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

        PropertyDetailsReadModel? property = await _propertyReadService
            .GetByIdAsync(query.PropertyId, cancellationToken);

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

        IReadOnlyList<AvailableRentableUnitCandidateReadModel> candidates = await _availabilityReadService
            .GetAvailableUnitsAsync(
                query.PropertyId,
                stayPeriod.CheckInDate,
                stayPeriod.CheckOutDate,
                guestCount.Value,
                cancellationToken);

        IReadOnlyList<AvailabilityPricingSeasonReadModel> pricingSeasons = candidates.Count == 0
            ? Array.Empty<AvailabilityPricingSeasonReadModel>()
            : await _availabilityReadService.GetPricingSeasonsAsync(
                candidates.Select(unit => unit.Id).ToArray(),
                stayPeriod.CheckInDate,
                stayPeriod.CheckOutDate,
                cancellationToken);

        Dictionary<Guid, AvailabilityPricingSeasonReadModel[]> seasonsByUnit = pricingSeasons
            .GroupBy(season => season.RentableUnitId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var quotedUnits = new List<AvailableRentableUnitReadModel>(candidates.Count);

        foreach (AvailableRentableUnitCandidateReadModel candidate in candidates)
        {
            seasonsByUnit.TryGetValue(candidate.Id, out AvailabilityPricingSeasonReadModel[]? unitSeasonRows);

            Result<AvailabilityQuoteReadModel> quoteResult = CalculateQuote(
                candidate,
                unitSeasonRows ?? Array.Empty<AvailabilityPricingSeasonReadModel>(),
                guestCount,
                stayPeriod);

            if (quoteResult.IsFailure)
            {
                return Result<AvailabilityReadModel>.Failure(quoteResult.Error);
            }

            quotedUnits.Add(
                new AvailableRentableUnitReadModel
                (
                    candidate.Id,
                    candidate.Name,
                    candidate.Type,
                    candidate.MaximumCapacity,
                    candidate.IsEntireProperty,
                    quoteResult.Value
                ));
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

    private static Result<AvailabilityQuoteReadModel> CalculateQuote(
        AvailableRentableUnitCandidateReadModel candidate,
        AvailabilityPricingSeasonReadModel[] seasonRows,
        GuestCount guestCount,
        StayPeriod stayPeriod)
    {
        Result<Money> regularRateResult = Money.Create(
            candidate.RegularNightlyRateAmount,
            candidate.PricingCurrency);

        if (regularRateResult.IsFailure)
        {
            return Result<AvailabilityQuoteReadModel>.Failure(regularRateResult.Error);
        }

        Result<Money> weekendRateResult = Money.Create(
            candidate.WeekendNightlyRateAmount,
            candidate.PricingCurrency);

        if (weekendRateResult.IsFailure)
        {
            return Result<AvailabilityQuoteReadModel>.Failure(weekendRateResult.Error);
        }

        Result<Money> extraGuestRateResult = Money.Create(
            candidate.ExtraGuestNightlyRateAmount,
            candidate.PricingCurrency);

        if (extraGuestRateResult.IsFailure)
        {
            return Result<AvailabilityQuoteReadModel>.Failure(extraGuestRateResult.Error);
        }

        Result<RentableUnitPricing> pricingResult = RentableUnitPricing.Create(
            regularRateResult.Value,
            weekendRateResult.Value,
            extraGuestRateResult.Value);

        if (pricingResult.IsFailure)
        {
            return Result<AvailabilityQuoteReadModel>.Failure(pricingResult.Error);
        }

        var seasons = new List<PricingSeason>(seasonRows.Length);

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
            pricingResult.Value,
            candidate.MaxBaseGuests,
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
