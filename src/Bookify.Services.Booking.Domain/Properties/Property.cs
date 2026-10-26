using Bookify.Services.Booking.Domain.Properties.Errors;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Properties;

public sealed class Property
{
    private const int MaxNameLength = 200;
    private const int MaxTimeZoneIdLength = 100;
    private Property()
    {
        Name = string.Empty;
        TimeZoneId = string.Empty;
        OwnerSubjectId = string.Empty;
    }

    private Property(
        Guid id,
        string name,
        string timeZoneId,
        TimeOnly checkInTime,
        TimeOnly checkOutTime,
        string ownerSubjectId)
    {
        Id = id;
        Name = name;
        TimeZoneId = timeZoneId;
        CheckInTime = checkInTime;
        CheckOutTime = checkOutTime;
        OwnerSubjectId = ownerSubjectId;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string TimeZoneId { get; private set; }
    public TimeOnly CheckInTime { get; private set; }
    public TimeOnly CheckOutTime { get; private set; }
    public string OwnerSubjectId { get; private set; }
    public bool IsActive { get; private set; }

    public static Result<Property> Create(
        string name,
        string timeZoneId,
        TimeOnly checkInTime,
        TimeOnly checkOutTime,
        string ownerSubjectId)
    {
        string? normalizedName = name?.Trim();
        string? normalizedTimeZoneId = timeZoneId?.Trim();

        if (!IsValidName(normalizedName))
        {
            return Result<Property>.Failure(PropertyErrors.InvalidName);
        }

        if (!IsValidTimeZoneId(normalizedTimeZoneId))
        {
            return Result<Property>.Failure(PropertyErrors.InvalidTimeZoneId);
        }

        if (string.IsNullOrWhiteSpace(ownerSubjectId) ||
            ownerSubjectId.Length > 255 ||
            !string.Equals(ownerSubjectId, ownerSubjectId.Trim(), StringComparison.Ordinal))
        {
            return Result<Property>.Failure(PropertyErrors.InvalidOwnerSubjectId);
        }

        var property = new Property(
            Guid.NewGuid(),
            normalizedName!,
            normalizedTimeZoneId!,
            checkInTime,
            checkOutTime,
            ownerSubjectId);

        return Result<Property>.Success(property);
    }

    public Result Rename(string name)
    {
        string? normalizedName = name?.Trim();

        if (!IsValidName(name))
        {
            return Result.Failure(PropertyErrors.InvalidName);
        }

        Name = normalizedName!;

        return Result.Success();
    }

    public Result ChangeTimeZoneId(string timeZoneId)
    {
        string? normalizedTimeZoneId = timeZoneId?.Trim();

        if (!IsValidTimeZoneId(normalizedTimeZoneId))
        {
            return Result.Failure(PropertyErrors.InvalidTimeZoneId);
        }

        TimeZoneId = normalizedTimeZoneId!;

        return Result.Success();
    }

    public void UpdateStaySchedule(TimeOnly checkInTime, TimeOnly checkOutTime)
    {
        CheckInTime = checkInTime;
        CheckOutTime = checkOutTime;
    }

    public Result UpdateDetails(
        string name,
        string timeZoneId,
        TimeOnly checkInTime,
        TimeOnly checkOutTime)
    {
        string? normalizedName = name?.Trim();
        string? normalizedTimeZoneId = timeZoneId?.Trim();

        if (!IsValidName(normalizedName))
        {
            return Result.Failure(PropertyErrors.InvalidName);
        }

        if (!IsValidTimeZoneId(normalizedTimeZoneId))
        {
            return Result.Failure(PropertyErrors.InvalidTimeZoneId);
        }

        Name = normalizedName!;
        TimeZoneId = normalizedTimeZoneId!;
        CheckInTime = checkInTime;
        CheckOutTime = checkOutTime;

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

    private static bool IsValidName(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && name.Length <= MaxNameLength;
    }

    private static bool IsValidTimeZoneId(string? timeZoneId)
    {
        return !string.IsNullOrWhiteSpace(timeZoneId) &&
            timeZoneId.Length <= MaxTimeZoneIdLength &&
            TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _);
    }
}
