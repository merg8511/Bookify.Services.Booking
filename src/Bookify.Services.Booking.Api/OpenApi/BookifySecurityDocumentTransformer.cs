using Bookify.Services.Booking.Api.Security;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Bookify.Services.Booking.Api.OpenApi;

internal sealed class BookifySecurityDocumentTransformer : IOpenApiDocumentTransformer
{
    internal const string BearerScheme = "Bearer";
    internal const string GuestScheme = "BookingGuestToken";
    internal const string StripeScheme = "StripeSignature";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "OIDC JWT access token. " +
            "The server validates the issuer, audience, lifetime and signature."
        };

        document.Components.SecuritySchemes[GuestScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = BookingGuestAccessHeader.Name,
            Description = "Opaque credential for accessing an anonymous booking. Issued once in the initial successful" +
            "booking creation response. Store securely. The credential cannot be recovered from the API."
        };

        document.Components.SecuritySchemes[StripeScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = "Stripe-Signature",
            Description = "Stripe webhook signature. The signature is cryptographically verified against the original " +
            "request body and configured webhook secret. This is not a JWT or a reusable API token."
        };

        return Task.CompletedTask;
    }
}
