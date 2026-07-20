using Bookify.Services.Booking.Domain.Properties.Errors;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Properties
{
    public sealed class RentableUnit
    {
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

        public Guid Id { get; }
        public Guid PropertyId { get; }
        public string Name { get; private set; }
        public RentableUnitType Type { get; }
        public int MaximumCapacity { get; private set; }
        public int MaxBaseGuests { get; private set; }
        public bool IsActive { get; private set; }

        public bool isEntireProperty =>
            Type == RentableUnitType.EntireProperty;

        public static Result<RentableUnit> Create(
            Guid propertyId,
            string name,
            RentableUnitType type,
            int maximumCapacity,
            int maxBaseGuests)
        {
            if (propertyId == Guid.Empty)
                return Result<RentableUnit>.Failure(
                    RentableUnitErrors.InvalidPropertyId);

            string? normalizedName = name?.Trim();

            if (string.IsNullOrWhiteSpace(normalizedName))
                return Result<RentableUnit>.Failure(
                    RentableUnitErrors.InvalidName);

            if (!Enum.IsDefined(typeof(RentableUnitType), type))
                return Result<RentableUnit>.Failure(
                    RentableUnitErrors.InvalidType);

            Result capacityValidation = ValidateCapacity(
                maximumCapacity,
                maxBaseGuests);

            if (capacityValidation.IsFailure)
                return Result<RentableUnit>.Failure(
                    capacityValidation.Error);

            var rentableUnit = new RentableUnit(
                Guid.NewGuid(),
                propertyId,
                normalizedName,
                type,
                maximumCapacity,
                maxBaseGuests);

            return Result<RentableUnit>.Success(rentableUnit);
        }

        public Result Rename(string name)
        {
            string? normalizedName = name?.Trim();

            if (string.IsNullOrWhiteSpace(normalizedName))
                return Result.Failure(
                    RentableUnitErrors.InvalidName);

            Name = normalizedName;

            return Result.Success();
        }

        public Result UpdateCapacity(
            int maximumCapacity,
            int maxBaseGuests)
        {
            Result validationResult = ValidateCapacity(
                maximumCapacity,
                maxBaseGuests);

            if (validationResult.IsFailure)
                return validationResult;

            MaximumCapacity = maximumCapacity;
            MaxBaseGuests = maxBaseGuests;

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
                return false;

            if (Id == other.Id)
                return true;

            return isEntireProperty || other.isEntireProperty;
        }

        public static Result ValidateCapacity(
            int maximumCapacity,
            int maxBaseGuests)
        {
            if (maximumCapacity <= 0)
                return Result.Failure(
                    RentableUnitErrors.InvalidMaximumCapacity);

            if (maxBaseGuests <= 0)
                return Result.Failure(
                    RentableUnitErrors.InvalidMaxBaseGuest);

            if (maxBaseGuests > maximumCapacity)
                return Result.Failure(
                    RentableUnitErrors.BaseGuestsExceedCapacity);

            return Result.Success();
        }
    }
}
