# ADR-0008: Use external OIDC JWT identity with resource-scoped and Guest authorization

> **Status:** Accepted — retrospective documentation of the implemented architecture  
> **Recorded:** 2026-11-04 · **Scope:** Bookify.Services.Booking backend · **Module:** 24.7  
> **Decision date:** Not independently established. The recorded date documents the decision; it is not claimed to be the original adoption date.

## Context

Bookify has Owner, Admin and Customer access patterns, but guest checkout must work without forcing account creation. Role checks alone are insufficient: a token belonging to one owner must not grant access to another owner’s properties or unrelated bookings. Stripe webhook authentication is independent of customer identity.

## Decision

Trust an external HTTPS **OIDC authority** for JWT authentication, validating issuer, audience, lifetime, signature and required subject claim. Keep authorization policies for roles and resource ownership. Allow an anonymous booking to receive a one-time opaque `Booking-Guest-Token`; persist its hash, never the raw token. Booking/customer endpoints may authorize a matching authenticated customer **or** a valid guest credential bound to the booking. The Stripe webhook instead requires cryptographic `Stripe-Signature` verification of the original body.

## Alternatives considered (retrospective)

The following are trade-off analyses, **not** a claim that a historical design meeting formally voted on every option.

| Alternative | Trade-off for Bookify |
| --- | --- |
| Build a local user/password database | Adds credential lifecycle, reset flows and password-handling obligations. |
| Require accounts for all bookings | Simplifies one access mode, but breaks the supported Guest checkout workflow. |
| Use booking reference as a secret | References are lookup identifiers, not secret credentials or ownership proof. |

## Consequences and trade-offs

**Benefits:** centralized identity lifecycle, clear ownership boundaries and low-friction guest reservations. **Costs:** identity-provider configuration, policy complexity and heightened responsibility for safeguarding guest tokens. **One-time secret:** the raw Guest token is returned in the initial successful booking response header; idempotent replay cannot recover it from storage. **No new auth endpoint:** this service does not issue or refresh OIDC tokens.

## Verification and review triggers

- Preserve tests for JWT validity, roles, cross-owner denial, guest success and forged token denial.
- Validate OpenAPI alternative security (`Bearer` **or** `BookingGuestToken`) and the Stripe scheme in 24.8.
- Review issuer/audience configuration and CORS exposure in every environment.
- Reevaluate only when a new identity or client-authentication requirement is formally defined.

## Source of truth

- [`IdentityDependencyInjection.cs`](../../../src/Bookify.Services.Booking.Api/Security/IdentityDependencyInjection.cs) — JWT validation and OIDC setup.
- [`AuthorizationDependencyInjection.cs`](../../../src/Bookify.Services.Booking.Api/Security/Authorization/AuthorizationDependencyInjection.cs) — resource policies.
- [`EndpointAccessExtensions.cs`](../../../src/Bookify.Services.Booking.Api/Security/EndpointAccessExtensions.cs) — endpoint access metadata.
- [`BookingGuestAccessHeader.cs`](../../../src/Bookify.Services.Booking.Api/Security/BookingGuestAccessHeader.cs) — guest request header.
- [`Booking.cs`](../../../src/Bookify.Services.Booking.Domain/Bookings/Booking.cs) — hashed guest credential ownership.
- [`CreateBookingEndpoint.cs`](../../../src/Bookify.Services.Booking.Api/Endpoints/Bookings/Create/CreateBookingEndpoint.cs) — one-time credential response.
