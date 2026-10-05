# Bookify — Rate Limiting

## Purpose

Bookify applies endpoint-specific rate limiting to operations whose
abuse profile differs significantly.

Rate limiting complements authentication, authorization, provider
signature validation and infrastructure-level DDoS protection.

It is not a replacement for a WAF or provider-level DDoS protection.

## Policies

### Public reads

Policy:

public-reads

Applies to:

- GET /api/v1/properties
- GET /api/v1/properties/{propertyId}
- GET /api/v1/properties/{propertyId}/units
- GET /api/v1/properties/{propertyId}/availability

Default:

120 requests per minute.

Partition:

- authenticated subject when available;
- otherwise remote IP address.

### Booking creation

Policy:

booking-creation

Applies to:

- POST /api/v1/bookings

Default:

10 requests per minute.

Partition:

- authenticated subject when available;
- otherwise remote IP address.

### Payment initiation

Policy:

payment-initiation

Applies to:

- POST /api/v1/payments

Default:

6 requests per minute.

Partition:

1. authenticated subject;
2. SHA-256 fingerprint of the guest booking credential;
3. remote IP as fallback.

The raw Booking-Guest-Token is never used as a partition key.

### Login-facing operations

Policy:

login-facing

Default:

5 requests per minute per remote IP.

Bookify.Services.Booking currently exposes no login, password or token
issuance endpoint because authentication is delegated to the external
OIDC provider.

The policy is registered but is not currently attached to an endpoint.

### Provider webhook

Policy:

webhook

Applies to:

- POST /api/v1/payments/webhooks/stripe

Default:

300 requests per minute per remote IP.

The higher limit allows legitimate provider retry and burst behavior.

Stripe-Signature validation remains mandatory after the request passes
the rate limiter.

## Rejection behavior

Rejected requests return:

HTTP 429 Too Many Requests

The response uses application/problem+json and the Bookify code:

RateLimit.Exceeded

When the limiter can calculate the next available permit, the response
also includes:

Retry-After

No request queue is used.

## Middleware order

The Booking API processes relevant requests in this order:

Routing
Authentication
Rate Limiting
Authorization
Idempotency
Endpoint

Rate limiting therefore has access to the authenticated principal and
can reject excessive traffic before resource authorization,
idempotency persistence and business execution.

## Reverse proxies

Rate limiting uses HttpContext.Connection.RemoteIpAddress for anonymous
and provider traffic.

Trusted forwarded headers are configured separately in module 17.8.

Do not trust X-Forwarded-For directly without trusted proxy/network
configuration.

## Operations

The configured limits are starting operational defaults.

They must be reviewed against production metrics, legitimate traffic
patterns, load tests and provider webhook behavior before final
production tuning.
