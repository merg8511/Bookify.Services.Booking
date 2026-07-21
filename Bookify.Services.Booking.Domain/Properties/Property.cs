using Bookify.Services.Booking.Domain.Properties.Errors;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Properties;

public sealed class Property
{
    private Property(
        Guid id,
        string name,
        string timeZoneId,
        TimeOnly checkInTime,
        TimeOnly checkOutTime)
    {
        Id = id;
        Name = name;
        TimeZoneId = timeZoneId;
        CheckInTime = checkInTime;
        CheckOutTime = checkOutTime;
        IsActive = true;
    }

    public Guid Id { get; }
    public string Name { get; private set; }
    public string TimeZoneId { get; private set; }
    public TimeOnly CheckInTime { get; private set; }
    public TimeOnly CheckOutTime { get; private set; }
    public bool IsActive { get; private set; }

    public static Result<Property> Create(
        string name,
        string timeZoneId,
        TimeOnly checkInTime,
        TimeOnly checkOutTime)
    {
        string? normalizedName = name?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return Result<Property>.Failure(
                PropertyErrors.InvalidName);
        }

        string? normalizedTimeZoneId = timeZoneId?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedTimeZoneId))
        {
            return Result<Property>.Failure(
                PropertyErrors.InvalidTimeZoneId);
        }

        var property = new Property(
            Guid.NewGuid(),
            normalizedName,
            normalizedTimeZoneId,
            checkInTime,
            checkOutTime);

        return Result<Property>.Success(property);
    }

    public Result Rename(string name)
    {
        string? normalizedName = name?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return Result.Failure(
                PropertyErrors.InvalidName);
        }

        Name = normalizedName;

        return Result.Success();
    }

    public Result ChangeTimeZoneId(string timeZoneId)
    {
        string? normalizedTimeZoneId = timeZoneId?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedTimeZoneId))
        {
            return Result.Failure(
                PropertyErrors.InvalidTimeZoneId);
        }

        TimeZoneId = normalizedTimeZoneId;

        return Result.Success();
    }

    public void UpdateStaySchedule(
        TimeOnly checkInTime,
        TimeOnly checkOutTime)
    {
        CheckInTime = checkInTime;
        CheckOutTime = checkOutTime;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
