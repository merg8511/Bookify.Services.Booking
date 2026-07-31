using Bookify.Services.Booking.Application.Common.Sorting;

namespace Bookify.Services.Booking.Application.Properties.GetPaged;

internal static class PropertySorting
{
    private const string DefaultSortBy = "name";
    private const string DefaultSortDirection = "asc";

    internal static bool TryParseSortField(
        string? value,
        out PropertySortField sortField)
    {
        string candidate =
            string.IsNullOrWhiteSpace(value)
            ? DefaultSortBy
            : value.Trim();

        if (candidate.Equals(
            "name",
            StringComparison.OrdinalIgnoreCase))
        {
            sortField = PropertySortField.Name;

            return true;
        }

        if (candidate.Equals(
            "isActive",
            StringComparison.OrdinalIgnoreCase))
        {
            sortField = PropertySortField.IsActive;
            return true;
        }

        sortField = default;
        return false;
    }

    internal static bool TryParseSortDirection(
        string? value,
        out SortDirection sortDirection)
    {
        string candidate = string.IsNullOrWhiteSpace(value)
            ? DefaultSortDirection
            : value.Trim();

        if (candidate.Equals(
            "asc",
            StringComparison.OrdinalIgnoreCase))
        {
            sortDirection = SortDirection.Ascending;
            return true;
        }

        if (candidate.Equals(
            "desc",
            StringComparison.OrdinalIgnoreCase))
        {
            sortDirection = SortDirection.Descending;

            return true;
        }

        sortDirection = default;
        return false;
    }

    internal static PropertySortField ParseSortField(string? value)
    {
        if (TryParseSortField(
            value,
            out PropertySortField sortField))
        {
            return sortField;
        }

        throw new InvalidOperationException(
            "The property sort field must be " +
            "validated before handling the query.");
    }

    internal static SortDirection ParseSortDirection(string? value)
    {
        if (TryParseSortDirection(value, out SortDirection sortDirection))
        {
            return sortDirection;
        }

        throw new InvalidOperationException(
            "The sort direction must be " +
            "validated before handling the query.");
    }
}
