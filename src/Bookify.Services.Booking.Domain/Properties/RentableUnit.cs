using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Properties.Errors;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Properties;

public sealed class RentableUnit
{
    private const int MaxNameLength = 200;
    private readonly List<PricingSeason> _pricingSeasons = [];

    private RentableUnit()
    {
        Name = string.Empty;
    }

    private RentableUnit(
        Guid id,
        Guid propertyId,
        string name,
        RentableUnitType type,
        int maximumCapacity,
        int maxBaseGuests)
    {
        Id = id;
        PropertyId = propertyId;
        Name = name;
        Type = type;
        MaximumCapacity = maximumCapacity;
        MaxBaseGuests = maxBaseGuests;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid PropertyId { get; private set; }
    public string Name { get; private set; }
    public RentableUnitType Type { get; private set; }
    public int MaximumCapacity { get; private set; }
    public int MaxBaseGuests { get; private set; }
    public RentableUnitPricing? Pricing { get; private set; }
    public IReadOnlyCollection<PricingSeason> PricingSeasons => _pricingSeasons.AsReadOnly();
    public bool IsActive { get; private set; }

    public bool IsEntireProperty =>
        Type == RentableUnitType.EntireProperty;

    public static Result<RentableUnit> Create(
        Guid propertyId,
        string name,
        RentableUnitType type,
        int maximumCapacity,
        int maxBaseGuests)
    {
        if (propertyId == Guid.Empty)
        {
            return Result<RentableUnit>.Failure(RentableUnitErrors.InvalidPropertyId);
        }

        string? normalizedName = name?.Trim();

        if (!IsValidName(normalizedName))
        {
            return Result<RentableUnit>.Failure(RentableUnitErrors.InvalidName);
        }

        if (!Enum.IsDefined(type))
        {
            return Result<RentableUnit>.Failure(RentableUnitErrors.InvalidType);
        }

        Result capacityValidation = ValidateCapacity(maximumCapacity, maxBaseGuests);

        if (capacityValidation.IsFailure)
        {
            return Result<RentableUnit>.Failure(capacityValidation.Error);
        }

        var rentableUnit = new RentableUnit(
            Guid.NewGuid(),
            propertyId,
            normalizedName!,
            type,
            maximumCapacity,
            maxBaseGuests);

        return Result<RentableUnit>.Success(rentableUnit);
    }

    public Result Rename(string name)
    {
        string? normalizedName = name?.Trim();

        if (!IsValidName(normalizedName))
        {
            return Result.Failure(RentableUnitErrors.InvalidName);
        }

        Name = normalizedName!;

        return Result.Success();
    }

    public Result UpdateCapacity(int maximumCapacity, int maxBaseGuests)
    {
        Result validationResult = ValidateCapacity(maximumCapacity, maxBaseGuests);

        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        MaximumCapacity = maximumCapacity;
        MaxBaseGuests = maxBaseGuests;

        return Result.Success();
    }

    public Result UpdateDetails(
        string name,
        RentableUnitType type,
        int maximumCapacity,
        int maxBaseGuests)
    {
        string? normalizedName = name?.Trim();

        if (!IsValidName(normalizedName))
        {
            return Result.Failure(RentableUnitErrors.InvalidName);
        }

        if (!Enum.IsDefined(type))
        {
            return Result.Failure(RentableUnitErrors.InvalidType);
        }

        Result capacityValidation = ValidateCapacity(maximumCapacity, maxBaseGuests);

        if (capacityValidation.IsFailure)
        {
            return capacityValidation;
        }

        Name = normalizedName!;
        Type = type;
        MaximumCapacity = maximumCapacity;
        MaxBaseGuests = maxBaseGuests;

        return Result.Success();
    }

    public void ConfigurePricing(RentableUnitPricing pricing)
    {
        ArgumentNullException.ThrowIfNull(pricing);
        Pricing = pricing;
    }

    public void AddPricingSeason(PricingSeason pricingSeason)
    {
        ArgumentNullException.ThrowIfNull(pricingSeason);
        _pricingSeasons.Add(pricingSeason);
    }

    public Result AddManagedPricingSeason(PricingSeason pricingSeason)
    {
        ArgumentNullException.ThrowIfNull(pricingSeason);

        Result validation = ValidateManagedSeason(
            pricingSeason,
            excludedSeasonId: null,
            checkActiveOverlap: pricingSeason.IsActive);

        if (validation.IsFailure)
        {
            return validation;
        }

        _pricingSeasons.Add(pricingSeason);

        return Result.Success();
    }

    public Result UpdateManagedPricingSeason(
        int seasonId,
        DateOnly startDate,
        DateOnly endDate,
        Money nightlyRate,
        int priority)
    {
        ArgumentNullException.ThrowIfNull(nightlyRate);

        PricingSeason? existingSeason = _pricingSeasons.FirstOrDefault(season => season.Id == seasonId);

        if (existingSeason is null)
        {
            return Result.Failure(PricingSeasonErrors.NotFound(seasonId));
        }

        Result<PricingSeason> proposedResult = PricingSeason.Create(
            startDate,
            endDate,
            nightlyRate,
            priority);

        if (proposedResult.IsFailure)
        {
            return Result.Failure(proposedResult.Error);
        }

        Result validation = ValidateManagedSeason(
            proposedResult.Value,
            excludedSeasonId: seasonId,
            checkActiveOverlap: existingSeason.IsActive);

        if (validation.IsFailure)
        {
            return validation;
        }

        return existingSeason.Update(
            startDate,
            endDate,
            nightlyRate,
            priority);
    }

    public Result SetManagedPricingSeasonStatus(int seasonId, bool isActive)
    {
        PricingSeason? season = _pricingSeasons.FirstOrDefault(item => item.Id == seasonId);

        if (season is null)
        {
            return Result.Failure(PricingSeasonErrors.NotFound(seasonId));
        }

        if (season.IsActive == isActive)
        {
            return Result.Success();
        }

        if (!isActive)
        {
            season.Deactivate();

            return Result.Success();
        }

        Result validation = ValidateManagedSeason(
            season,
            excludedSeasonId: seasonId,
            checkActiveOverlap: true);

        if (validation.IsFailure)
        {
            return validation;
        }

        season.Activate();

        return Result.Success();
    }


    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public bool CanAccommodate(GuestCount guestCount)
    {
        ArgumentNullException.ThrowIfNull(guestCount);

        return guestCount.Value <= MaximumCapacity;
    }

    public bool SharesInventoryWith(RentableUnit other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (PropertyId != other.PropertyId)
        {
            return false;
        }

        if (Id == other.Id)
        {
            return true;
        }

        return IsEntireProperty || other.IsEntireProperty;
    }

    private Result ValidateManagedSeason(
        PricingSeason candidate,
        int? excludedSeasonId,
        bool checkActiveOverlap)
    {
        if (Pricing is null)
        {
            return Result.Failure(PricingSeasonErrors.PricingNotConfigured);
        }

        if (!Pricing.RegularNightlyRate.HasSameCurrency(candidate.NightlyRate))
        {
            return Result.Failure(PricingSeasonErrors.CurrencyMismatch);
        }

        if (!checkActiveOverlap)
        {
            return Result.Success();
        }

        foreach (PricingSeason existing in _pricingSeasons)
        {
            if (!existing.IsActive)
            {
                continue;
            }

            if (excludedSeasonId.HasValue && existing.Id == excludedSeasonId.Value)
            {
                continue;
            }

            if (existing.Priority != candidate.Priority)
            {
                continue;
            }

            bool overlaps = candidate.StartDate < existing.EndDate && candidate.EndDate > existing.StartDate;

            if (!overlaps)
            {
                continue;
            }

            DateOnly firstConflictNight = candidate.StartDate > existing.StartDate
                ? candidate.StartDate
                : existing.StartDate;

            return Result.Failure(PricingSeasonErrors.AmbiguousPriority(firstConflictNight, candidate.Priority));
        }

        return Result.Success();
    }

    private static bool IsValidName(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && name.Length <= MaxNameLength;
    }

    private static Result ValidateCapacity(
        int maximumCapacity,
        int maxBaseGuests)
    {
        if (maximumCapacity <= 0)
        {
            return Result.Failure(RentableUnitErrors.InvalidMaximumCapacity);
        }

        if (maxBaseGuests <= 0)
        {
            return Result.Failure(RentableUnitErrors.InvalidMaxBaseGuests);
        }

        if (maxBaseGuests > maximumCapacity)
        {
            return Result.Failure(RentableUnitErrors.BaseGuestsExceedCapacity);
        }

        return Result.Success();
    }
}
