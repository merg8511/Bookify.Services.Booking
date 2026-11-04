# Interactive API Reference — Scalar

> **Bookify Module 24.9** · ASP.NET Core 10 · Native OpenAPI 3.1 · Scalar.AspNetCore 2.18.7

## Why Scalar?

Bookify's **source of truth is still** the existing native ASP.NET Core OpenAPI document at `/openapi/v1.json`. Scalar is a browser UI that *reads* that document. It does not create routes, generate a competing contract, or replace the existing OpenAPI transformers.

The UI helps developers inspect HTTP methods, sample request/response bodies, authorization models, pagination, errors and operation IDs, and manually try supported requests.

## Availability

| Environment | `/scalar` | `/openapi/v1.json` |
| --- | --- | --- |
| Development | Yes | Yes |
| Testing | Yes (in-process integration tests) | Yes |
| Demo | No | No |
| Staging | No | No |
| Production | No | No |

These are endpoint-registration rules, not an authentication or networking substitute. If Development is made accessible over a public network, restrict that environment at the infrastructure/network layer.

## Run locally (Windows / PowerShell)

```powershell
dotnet restore Bookify.Services.Booking.slnx
dotnet dev-certs https --trust

dotnet run `
    --project src/Bookify.Services.Booking.Api `
    --launch-profile https
```

Requirements: the configured PostgreSQL connection string and a valid HTTPS OIDC authority/audience. Stripe or SMTP credentials are not required when using the Development `Fake` provider and SMTP disabled.

Open:

- **UI:** <https://localhost:7071/scalar>
- **Raw contract:** <https://localhost:7071/openapi/v1.json>

The HTTPS launch profile opens Scalar in the browser automatically when the local profile is used.

## Using the API client

1. Choose a group such as **Properties**, **Bookings**, **Payments** or **Admin Bookings**.
2. Inspect the operation ID, request body and responses.
3. For public reads, invoke a suitable `GET` and inspect the HTTP response.
4. For Owner/Admin operations, supply your own **test JWT** using Scalar's Bearer security scheme. Do not hardcode the JWT in source or Scalar options.
5. For Customer/Guest booking operations, choose **Bearer OR BookingGuestToken** and provide only the credential applicable to the reservation. Guest credentials are obtained from the `Booking-Guest-Token` header of the initial `201 Created` response; they cannot be recovered later.
6. For `POST /api/v1/bookings` and `POST /api/v1/payments`, provide a fresh `Idempotency-Key` for a new logical operation. Reuse it only to safely retry that same request.
7. **Do not manually test** the Stripe webhook by inventing a signature: the HMAC header must match the *exact raw request body*. Use [the Stripe CLI guide](../payments/stripe-cli-local-webhooks.md) to test it correctly.

The built-in API client calls actual endpoints and can write to the Development database. Prefer read-only requests while exploring, and use intentionally disposable test data for writes.

## Security boundary and content security policy

Bookify's ordinary responses keep this restrictive CSP:

```text
default-src 'none'; frame-ancestors 'none'; base-uri 'none'
```

That policy blocks interactive documentation JavaScript. Scalar therefore generates a **fresh cryptographic nonce per request**. `SecurityHeadersMiddleware` reads the nonce from `HttpContext.Items` and applies a **response-specific** CSP to Scalar's HTML only:

```text
default-src 'none';
script-src 'self' 'nonce-{dynamic-value}';
style-src 'self' 'unsafe-inline';
connect-src 'self';
img-src 'self' data:;
font-src 'self';
worker-src 'self' blob:;
object-src 'none';
frame-ancestors 'none';
base-uri 'none'
```

The nonce is **not** static or configured as a secret. It appears in the response header and inline script markup and must change on every response. Scalar responses use `Cache-Control: no-store`.

`script-src` does **not** use `'unsafe-inline'` or `'unsafe-eval'`. Inline styles are necessary for Scalar's UI and are permitted only in the Scalar HTML response. The integration disables Scalar's Agent and default CDN fonts; the package's standard local JavaScript assets are used. No access tokens or provider secrets are preconfigured in the page.

Do not weaken the global CSP to accommodate the documentation interface.

## Regression

The existing [OpenAPI contract tests](../../tests/Bookify.Services.Booking.Integration.Tests/Endpoints/OpenApi/OpenApiDocumentContractTests.cs) protect the API's 33 operations and special security requirements.

The new `ScalarApiReferenceTests` check the UI route, per-request nonce isolation and preservation of strict CSP for ordinary endpoints. A human browser check should additionally confirm that the UI loads correctly without CSP violations in DevTools and that the OpenAPI document is rendered.

```powershell
dotnet build Bookify.Services.Booking.slnx -c Release

dotnet test tests/Bookify.Services.Booking.Integration.Tests/Bookify.Services.Booking.Integration.Tests.csproj `
    -c Release `
    --filter "FullyQualifiedName~ScalarApiReferenceTests|FullyQualifiedName~OpenApiDocumentContractTests"
```

> These integration tests use the existing `BookingApiFactory` and **require Docker** for PostgreSQL via Testcontainers.

## Technical sources

- [Microsoft: generate OpenAPI documents](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0)
- [Scalar: ASP.NET Core integration](https://scalar.com/products/api-references/integrations/aspnetcore/integration)
- [NuGet: Scalar.AspNetCore 2.18.7](https://www.nuget.org/packages/Scalar.AspNetCore/2.18.7)
