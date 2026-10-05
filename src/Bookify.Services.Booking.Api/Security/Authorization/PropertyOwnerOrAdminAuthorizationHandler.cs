using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Abstractions.Security;
using Bookify.Services.Booking.Domain.Properties;
using Microsoft.AspNetCore.Authorization;

namespace Bookify.Services.Booking.Api.Security.Authorization;

internal sealed class PropertyOwnerOrAdminAuthorizationHandler :
    AuthorizationHandler<PropertyOwnerOrAdminRequirement>
{
    private readonly ICurrentUser _currentUser;
    private readonly BookingAuthorizationResolver _bookingResolver;
    private readonly IPropertyRepository _propertyRepository;

    public PropertyOwnerOrAdminAuthorizationHandler(
        ICurrentUser currentUser,
        BookingAuthorizationResolver bookingResolver,
        IPropertyRepository propertyRepository)
    {
        _currentUser = currentUser ??
            throw new ArgumentNullException(nameof(currentUser));

        _bookingResolver = bookingResolver
            ?? throw new ArgumentNullException(nameof(bookingResolver));

        _propertyRepository = propertyRepository
            ?? throw new ArgumentNullException(nameof(propertyRepository));
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PropertyOwnerOrAdminRequirement requirement)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return;
        }

        if (_currentUser.Roles.Contains(BookifyRoles.Admin, StringComparer.Ordinal))
        {
            context.Succeed(requirement);
            return;
        }

        if (!_currentUser.Roles.Contains(BookifyRoles.Owner, StringComparer.Ordinal) ||
            string.IsNullOrWhiteSpace(_currentUser.Subject))
        {
            return;
        }

        if (context.Resource is not HttpContext httpContext)
        {
            return;
        }

        BookingAuthorizationResolution resolution = await _bookingResolver.ResolveAsync(httpContext);

        if (!resolution.HasMetadata)
        {
            return;
        }

        if (!resolution.HasIdentifier ||
            resolution.Booking is null)
        {
            context.Succeed(requirement);
            return;
        }

        Property? property = await _propertyRepository.GetByIdAsync(
            resolution.Booking.PropertyId,
            httpContext.RequestAborted);

        if (property is null)
        {
            return;
        }

        if (string.Equals(property.OwnerSubjectId, _currentUser.Subject, StringComparison.Ordinal))
        {
            context.Succeed(requirement);
        }
    }
}
