using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Properties.GetPaged;

internal static class GetPropertiesQueryErrors
{
    internal static readonly Error InvalidSortBy =
        new(
            "Properties.InvalidSortBy",
            "Sort field must be one of: name, isActive.",
            ErrorType.Validation);

    internal static readonly Error InvalidSortDirection =
        new(
            "Sorting.InvalidDirection",
            "Sort direction must be either 'asc' or 'desc'.",
            ErrorType.Validation);
}
