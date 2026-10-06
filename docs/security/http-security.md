# Bookify — HTTP Security

## CORS

Bookify never uses AllowAnyOrigin.

Browser access is restricted to explicitly configured origins through:

HttpSecurity:TrustedFrontendOrigins

An empty list denies all cross-origin browser access.

The API exposes these response headers to trusted browser clients:

- Booking-Guest-Token
- Location
- Retry-After

Bookify does not use cookie authentication and does not enable
cross-origin credentials.

## HTTPS

Outside the Testing environment, HTTP requests are redirected to HTTPS.

HSTS is enabled outside Development and Testing.

When TLS terminates at a reverse proxy, forwarded headers must run
before HTTPS redirection so ASP.NET Core observes the original HTTPS
scheme.

## Forwarded headers

Only:

- X-Forwarded-For
- X-Forwarded-Proto

are processed.

Forwarded headers are accepted only from addresses and networks
configured in:

HttpSecurity:ForwardedHeaders:KnownProxies
HttpSecurity:ForwardedHeaders:KnownNetworks

Do not clear the trusted proxy lists to accept every proxy.

Do not rely on ASPNETCORE_FORWARDEDHEADERS_ENABLED as a replacement
for explicit trusted proxy configuration.

## Security headers

Bookify returns:

- X-Content-Type-Options: nosniff
- X-Frame-Options: DENY
- Referrer-Policy: no-referrer
- Permissions-Policy
- Content-Security-Policy

Customer/guest resources also return:

Cache-Control: no-store
Pragma: no-cache

## Secrets

Secrets must never be committed to appsettings files or source code.

Development secrets may use ASP.NET Core User Secrets.

Production secrets must be supplied externally by the hosting
environment or an approved secret-management service.

Sensitive configuration includes:

- ConnectionStrings:Database
- Payments:Stripe:SecretKey
- Payments:Stripe:WebhookSecret

Identity Authority and Audience are configuration values rather than
passwords, but should still be environment-specific.

## Logging

Do not log:

- Authorization
- Booking-Guest-Token
- Idempotency-Key
- Stripe SecretKey
- Stripe WebhookSecret
- Stripe client_secret
- database credentials

EF Core SensitiveDataLogging is explicitly disabled.

JWT IncludeErrorDetails remains disabled.

HTTP request/response body logging is not enabled.

Advanced logging, tracing and metrics remain assigned to Module 19,
but that module must preserve these redaction rules.

## Idempotency security

Idempotency is scoped by:

caller_scope + HTTP method + endpoint + Idempotency-Key

caller_scope is a SHA-256 fingerprint derived from the authenticated
subject, guest access credential or effective remote IP.

Raw identity values and raw guest credentials are not persisted in the
idempotency table.

Forwarded headers run before idempotency scope resolution so the
effective remote IP can be trusted when the configured proxy is trusted.
