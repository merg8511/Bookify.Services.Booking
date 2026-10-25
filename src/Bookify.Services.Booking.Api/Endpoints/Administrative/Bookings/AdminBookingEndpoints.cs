using Bookify.Services.Booking.Api.Contracts.Pagination;
using Bookify.Services.Booking.Api.Endpoints.Administrative.Bookings.Actions;
using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Api.Security;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Bookings.Administrative.GetById;
using Bookify.Services.Booking.Application.Bookings.Administrative.GetPaged;
using Bookify.Services.Booking.Application.Bookings.Administrative.ReadModels;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Application.Common.Pagination;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bookify.Services.Booking.Api.Endpoints.Administrative.Bookings;

internal static class AdminBookingEndpoints
{
    public static void Map(RouteGroupBuilder apiGroup)
    {
        RouteGroupBuilder bookingsGroup = apiGroup
            .MapGroup("/admin/properties/{propertyId:guid}/bookings")
            .WithTags("Admin Bookings");

        bookingsGroup
            .MapGet("/", GetPagedAsync)
            .WithName(EndpointNames.AdminBookings.List)
            .WithSummary("Get filtered and paged administrative bookings.")
            .RequirePropertyOwnerOrAdminAccessFromRoute("propertyId")
            .Produces<PagedResponse<AdminBookingListItemResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        bookingsGroup
            .MapGet("/{bookingId:guid}", GetByIdAsync)
            .WithName(EndpointNames.AdminBookings.GetById)
            .WithSummary("Gets the administrative details of a booking.")
            .RequirePropertyOwnerOrAdminAccessFromRoute("propertyId")
            .Produces<AdminBookingDetailsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        AdminBookingActionsEndpoints.Map(bookingsGroup);
    }

    private static async Task<Results<Ok<PagedResponse<
        AdminBookingListItemResponse>>, ProblemHttpResult>> GetPagedAsync(
        Guid propertyId,
        int? pageNumber,
        int? pageSize,
        string? status,
        DateOnly? rangeStart,
        DateOnly? rangeEnd,
        Guid? unitId,
        string? customerSubjectId,
        IQueryExecutor<GetAdminBookingsQuery, PagedResult<AdminBookingListItemReadModel>> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var query = new GetAdminBookingsQuery(
            propertyId,
            pageNumber ?? PaginationDefaults.DefaultPageNumber,
            pageSize ?? PaginationDefaults.DefaultPageSize,
            status,
            rangeStart,
            rangeEnd,
            unitId,
            customerSubjectId);

        Result<PagedResult<AdminBookingListItemReadModel>> result = await executor.ExecuteAsync(query, cancellationToken);

        return result.ToHttpResult(
            httpContext,
            page => TypedResults.Ok(MapPageToResponse(page)));
    }

    private static async Task<Results<Ok<AdminBookingDetailsResponse>, ProblemHttpResult>> GetByIdAsync(
        Guid propertyId,
        Guid bookingId,
        IQueryExecutor<GetAdminBookingByIdQuery, BookingDetailsReadModel> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var query = new GetAdminBookingByIdQuery(propertyId, bookingId);

        Result<BookingDetailsReadModel> result = await executor.ExecuteAsync(query, cancellationToken);

        return result.ToHttpResult(
            httpContext,
            booking => TypedResults.Ok(MapDetailToResponse(booking)));
    }

    private static PagedResponse<AdminBookingListItemResponse> MapPageToResponse(PagedResult<AdminBookingListItemReadModel> page)
    {
        AdminBookingListItemResponse[] items = page
            .Items
            .Select(booking => new AdminBookingListItemResponse(
                booking.Id,
                booking.BookingReference,
                booking.PropertyId,
                booking.RentableUnitId,
                booking.RentableUnitName,
                booking.CustomerSubjectId,
                booking.GuestFullName,
                booking.CheckInDate,
                booking.CheckOutDate,
                booking.GuestCount,
                booking.Status,
                booking.TotalPrice,
                booking.Currency,
                booking.CreatedAtUtc))
            .ToArray();

        return new PagedResponse<AdminBookingListItemResponse>(
            items,
            page.PageNumber,
            page.PageSize,
            page.TotalRecords,
            page.TotalPages);
    }

    private static AdminBookingDetailsResponse MapDetailToResponse(BookingDetailsReadModel booking)
    {
        return new AdminBookingDetailsResponse(
            booking.Id,
            booking.BookingReference,
            booking.PropertyId,
            booking.PropertyName,
            booking.RentableUnitId,
            booking.RentableUnitName,
            booking.CustomerSubjectId,
            booking.CheckInDate,
            booking.CheckOutDate,
            booking.NumberOfNights,
            booking.GuestCount,
            booking.GuestFullName,
            booking.GuestEmail,
            booking.GuestPhone,
            booking.AccommodationPrice,
            booking.ExtraGuestPrice,
            booking.TotalPrice,
            booking.Currency,
            booking.Status,
            booking.CancellationReason,
            booking.PaymentStatus,
            booking.CreatedAtUtc,
            booking.ApprovalDueAtUtc,
            booking.ApprovedAtUtc,
            booking.PaymentDueAtUtc,
            booking.PaidAtUtc,
            booking.CancelledAtUtc,
            booking.CompletedAtUtc);
    }
}
