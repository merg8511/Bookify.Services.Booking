using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Api.Security;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Bookings.Administrative.Calendar;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bookify.Services.Booking.Api.Endpoints.Administrative.Bookings.Calendar;

internal static class AdminBookingCalendarEndpoint
{
    public static void Map(RouteGroupBuilder apiGroup)
    {
        apiGroup
            .MapGet("/admin/properties/{propertyId:guid}/calendar", HandleAsync)
            .WithName(EndpointNames.AdminBookings.GetCalendar)
            .WithTags("Admin Bookings")
            .WithSummary("Gets the administrative booking calendar for a property.")
            .RequirePropertyOwnerOrAdminAccessFromRoute("propertyId")
            .Produces<GetAdminBookingCalendarResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok<GetAdminBookingCalendarResponse>, ProblemHttpResult>> HandleAsync(
        Guid propertyId,
        DateOnly? rangeStart,
        DateOnly? rangeEnd,
        Guid? unitId,
        bool? blocksInventory,
        IQueryExecutor<GetAdminBookingCalendarQuery, IReadOnlyList<BookingCalendarItemReadModel>> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var query = new GetAdminBookingCalendarQuery(
            propertyId,
            rangeStart,
            rangeEnd,
            unitId,
            blocksInventory);

        Result<IReadOnlyList<BookingCalendarItemReadModel>> result = await executor
            .ExecuteAsync(query, cancellationToken);

        return result.ToHttpResult(
            httpContext,
            calendar => TypedResults.Ok(MapToResponse(
                propertyId,
                rangeStart!.Value,
                rangeEnd!.Value,
                calendar)));
    }

    private static GetAdminBookingCalendarResponse MapToResponse(
        Guid propertyId,
        DateOnly rangeStart,
        DateOnly rangeEnd,
        IReadOnlyList<BookingCalendarItemReadModel> calendar)
    {
        AdminBookingCalendarItemResponse[] items = calendar
            .Select(booking => new AdminBookingCalendarItemResponse(
                booking.BookingId,
                booking.RentableUnitId,
                booking.RentableUnitName,
                booking.CheckInDate,
                booking.CheckOutDate,
                booking.GuestCount,
                booking.Status,
                booking.BlocksInventory))
            .ToArray();

        return new GetAdminBookingCalendarResponse(
            propertyId,
            rangeStart,
            rangeEnd,
            items);
    }
}
