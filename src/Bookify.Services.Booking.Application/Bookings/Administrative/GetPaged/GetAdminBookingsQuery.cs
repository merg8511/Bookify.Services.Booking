using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Bookings.Administrative.ReadModels;
using Bookify.Services.Booking.Application.Common.Pagination;

namespace Bookify.Services.Booking.Application.Bookings.Administrative.GetPaged;

public sealed record GetAdminBookingsQuery(
    Guid PropertyId,
    int PageNumber,
    int PageSize,
    string? Status,
    DateOnly? RangeStart,
    DateOnly? RangeEnd,
    Guid? UnitId,
    string? CustomerSubjectId) : IQuery<PagedResult<AdminBookingListItemReadModel>>;
