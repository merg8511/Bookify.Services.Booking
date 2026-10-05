namespace Bookify.Services.Booking.Api.Security.Authorization;

using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using System.Text.Json;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

internal sealed class BookingAuthorizationResolver
{
    private const int RequestBodyBufferThreshold = 1024;
    private const long RequestBodyBufferLimit = 16 * 1024;
    private readonly IBookingRepository _bookingRepository;
    private readonly IBookingReadService _bookingReadService;

    public BookingAuthorizationResolver(
        IBookingRepository bookingRepository,
        IBookingReadService bookingReadService)
    {
        _bookingRepository = bookingRepository ?? throw new ArgumentNullException(nameof(bookingRepository));
        _bookingReadService = bookingReadService ?? throw new ArgumentNullException(nameof(bookingReadService));
    }

    public async Task<BookingAuthorizationResolution> ResolveAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        BookingAuthorizationMetadata? metadata = httpContext
            .GetEndpoint()?.Metadata
            .GetMetadata<BookingAuthorizationMetadata>();

        if (metadata is null)
        {
            return BookingAuthorizationResolution.MissingMetadata();
        }

        return metadata.Source switch
        {
            BookingAuthorizationSource.RouteBookingId => await ResolveRouteBookingIdAsync(httpContext, metadata),
            BookingAuthorizationSource.RouteIdentifier => await ResolveRouteIdentifierAsync(httpContext, metadata),
            BookingAuthorizationSource.JsonBodyBookingId => await ResolveJsonBodyBookingIdAsync(httpContext, metadata),
            _ => BookingAuthorizationResolution.MissingIdentifier()
        };
    }

    private async Task<BookingAuthorizationResolution> ResolveRouteBookingIdAsync(
        HttpContext httpContext,
        BookingAuthorizationMetadata metadata)
    {
        object? rawValue = httpContext.Request.RouteValues[metadata.ValueName];

        if (!Guid.TryParse(rawValue?.ToString(), out Guid bookingId) ||
            bookingId == Guid.Empty)
        {
            return BookingAuthorizationResolution.MissingIdentifier();
        }

        DomainBooking? booking = await _bookingRepository.GetByIdAsync(bookingId, httpContext.RequestAborted);

        return BookingAuthorizationResolution.Resolved(booking);
    }

    private async Task<BookingAuthorizationResolution> ResolveRouteIdentifierAsync(
        HttpContext httpContext,
        BookingAuthorizationMetadata metadata)
    {
        string? rawIdentifier = httpContext.Request.RouteValues[metadata.ValueName]?.ToString();

        if (string.IsNullOrWhiteSpace(rawIdentifier))
        {
            return BookingAuthorizationResolution.MissingIdentifier();
        }

        string identifier = rawIdentifier.Trim();

        if (Guid.TryParse(identifier, out Guid bookingId))
        {
            if (bookingId == Guid.Empty)
            {
                return BookingAuthorizationResolution.MissingIdentifier();
            }

            DomainBooking? booking = await _bookingRepository.GetByIdAsync(bookingId, httpContext.RequestAborted);

            return BookingAuthorizationResolution.Resolved(booking);
        }

        var referenceResult = BookingReference.Create(identifier);

        if (referenceResult.IsFailure)
        {
            return BookingAuthorizationResolution.MissingIdentifier();
        }

        BookingDetailsReadModel? readModel = await _bookingReadService.GetByReferenceAsync(
            referenceResult.Value.Value,
            httpContext.RequestAborted);

        if (readModel is null)
        {
            return BookingAuthorizationResolution.Resolved(booking: null);
        }

        DomainBooking? domainBooking = await _bookingRepository.GetByIdAsync(readModel.Id, httpContext.RequestAborted);

        return BookingAuthorizationResolution.Resolved(domainBooking);
    }

    private async Task<BookingAuthorizationResolution> ResolveJsonBodyBookingIdAsync(
        HttpContext httpContext,
        BookingAuthorizationMetadata metadata)
    {
        Guid? bookingId = await TryReadGuidFromJsonBodyAsync(
            httpContext.Request,
            metadata.ValueName,
            httpContext.RequestAborted);

        if (!bookingId.HasValue || bookingId.Value == Guid.Empty)
        {
            return BookingAuthorizationResolution.MissingIdentifier();
        }

        DomainBooking? booking = await _bookingRepository.GetByIdAsync(bookingId.Value, httpContext.RequestAborted);

        return BookingAuthorizationResolution.Resolved(booking);
    }

    private static async Task<Guid?> TryReadGuidFromJsonBodyAsync(
        HttpRequest request,
        string propertyName,
        CancellationToken cancellationToken)
    {
        request.EnableBuffering(RequestBodyBufferThreshold, RequestBodyBufferLimit);

        request.Body.Position = 0;

        try
        {
            using JsonDocument document = await JsonDocument.ParseAsync(
                request.Body,
                cancellationToken: cancellationToken);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                return property.Value.TryGetGuid(out Guid value)
                    ? value
                    : null;
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        finally
        {
            if (request.Body.CanSeek)
            {
                request.Body.Position = 0;
            }
        }
    }
}
