using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Common.Pagination;
using Bookify.Services.Booking.Application.Properties.ReadModels;

namespace Bookify.Services.Booking.Application.Properties.GetPaged;

public sealed record GetPropertiesQuery(
    int PageNumber,
    int PageSize,
    string? Name,
    bool? IsActive) : IQuery<PagedResult<PropertyListItemReadModel>>;
