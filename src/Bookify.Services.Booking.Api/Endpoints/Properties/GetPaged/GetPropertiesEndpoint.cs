using Bookify.Services.Booking.Api.Contracts.Pagination;
using Bookify.Services.Booking.Api.Endpoints;
using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Common.Pagination;
using Bookify.Services.Booking.Application.Properties.GetPaged;
using Bookify.Services.Booking.Application.Properties.ReadModels;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bookify.Services.Booking.Api.Endpoints.Properties.GetPaged;

internal static class GetPropertiesEndpoint
{
    public static void Map(
        RouteGroupBuilder propertiesGroup)
    {
        propertiesGroup
            .MapGet(
                "/",
                HandleAsync)
            .WithName(
                EndpointNames.Properties.List)
            .WithSummary(
                "Gets a filtered, sorted and paged list of properties.")
            .Produces<
                PagedResponse<
                    PropertyListItemResponse>>(
                        StatusCodes.Status200OK)
            .ProducesProblem(
                StatusCodes.Status400BadRequest)
            .ProducesProblem(
                StatusCodes.Status500InternalServerError);
    }

    private static async Task<
        Results<Ok<
            PagedResponse<PropertyListItemResponse>>,
            ProblemHttpResult>> HandleAsync(
                int? pageNumber,
                int? pageSize,
                string? name,
                bool? isActive,
                string? sortBy,
                string? sortDirection,
                IQueryExecutor<
                    GetPropertiesQuery,
                    PagedResult<
                        PropertyListItemReadModel>> queryExecutor,
                HttpContext httpContext,
                CancellationToken cancellationToken)
    {
        var query =
            new GetPropertiesQuery(
                pageNumber ?? PaginationDefaults.DefaultPageNumber,
                pageSize ?? PaginationDefaults.DefaultPageSize,
                name,
                isActive,
                sortBy,
                sortDirection);

        var result =
            await queryExecutor.ExecuteAsync(
                query,
                cancellationToken);

        return result.ToHttpResult(
            httpContext,
            page =>
                TypedResults.Ok(
                    MapToResponse(page)));
    }

    private static PagedResponse<
        PropertyListItemResponse> MapToResponse(
            PagedResult<PropertyListItemReadModel> page)
    {
        PropertyListItemResponse[] items =
            page.Items
                .Select(
                    property =>
                        new PropertyListItemResponse(
                            property.Id,
                            property.Name,
                            property.IsActive))
                .ToArray();

        return new PagedResponse<
            PropertyListItemResponse>(
            items,
            page.PageNumber,
            page.PageSize,
            page.TotalRecords,
            page.TotalPages);
    }
}
