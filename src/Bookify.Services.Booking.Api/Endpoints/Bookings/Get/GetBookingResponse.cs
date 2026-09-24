namespace Bookify.Services.Booking.Api.Endpoints.Bookings.Get;

public sealed record GetBookingResponse(
    Guid Id,
    string BookingReference,
    Guid PropertyId,
    string PropertyName,
    GetBookingRentableUnitResponse RentableUnitResponse,
    DateOnly CheckInDate,
    DateOnly CheckOutDate,
    int NumberOfNights,
    int GuestCount,
    GetBookingGuestResponse? Guest,
    GetBookingPriceResponse? Price,
    string Status,
    string? CancellationReason,
    string? PaymentStatus,
    DateTimeOffset? CreatedAtUtc,
    DateTimeOffset? ApprovalDueAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? PaymentDueAtUtc,
    DateTimeOffset? PaidAtUtc,
    DateTimeOffset? CancelledAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record GetBookingRentableUnitResponse(
    Guid Id,
    string Name);

public sealed record GetBookingGuestResponse(
    string FullName,
    string Email,
    string Phone);

public sealed record GetBookingPriceResponse(
    decimal AccommodationPrice,
    decimal ExtraGuestPrice,
    decimal TotalPrice,
    string Currency);
