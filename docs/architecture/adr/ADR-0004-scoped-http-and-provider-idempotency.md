# ADR-0004: Use scoped HTTP idempotency and durable payment operation identity

> **Status:** Accepted — retrospective documentation of the implemented architecture  
> **Recorded:** 2026-11-04 · **Scope:** Bookify.Services.Booking backend · **Module:** 24.7  
> **Decision date:** Not independently established. The recorded date documents the decision; it is not claimed to be the original adoption date.

## Context

Network retries, browser double-submits and provider timeouts can cause duplicate booking/payment commands. `POST /api/v1/bookings` and `POST /api/v1/payments` already require `Idempotency-Key`. A single global key alone would let unrelated callers interfere with one another. Payment initiation also has an external provider boundary where a local HTTP replay cache is insufficient.

## Decision

Scope the HTTP key by **caller fingerprint, HTTP method, route and key**. Compare the SHA-256 request-body hash before replay; reject mismatched payloads and concurrent in-progress attempts. Keep a persisted claim/completion record with the current **24-hour retention**. The middleware validates a single nonblank key of at most **255 characters**.

For payments, also reserve a durable operation identity before the Stripe call and use its idempotency key when creating a PaymentIntent. Do not persist or replay sensitive response bodies indiscriminately; a guest credential is only issued during initial creation, and payment `clientSecret` must be handled as sensitive data.

## Alternatives considered (retrospective)

The following are trade-off analyses, **not** a claim that a historical design meeting formally voted on every option.

| Alternative | Trade-off for Bookify |
| --- | --- |
| Client retry without idempotency | Can create duplicate bookings or payment attempts after uncertain responses. |
| In-memory request cache | Breaks on process restarts and multiple API instances. |
| Only Stripe-side idempotency | Does not protect database-side booking creation or HTTP caller collisions. |

## Consequences and trade-offs

**Benefits:** deterministic retry semantics within the documented scope; durable provider attempt identity. **Costs:** persistence and cleanup policy, distinction between an in-progress request and a completed one, and special handling of sensitive responses. **Limit:** the 24-hour HTTP retention is not a promise of infinite replay; a payment initiation recovery window can have separate rules.

## Verification and review triggers

- Test same-key same-payload, changed payload, concurrent claims, caller isolation and expiration.
- Verify `Idempotency-Key` appears only on applicable OpenAPI operations (completed in 24.2, regression in 24.8).
- Review every new side-effecting endpoint before opting it into the metadata.
- Revisit retention only with explicit product and storage requirements.

## Source of truth

- [`IdempotencyMiddleware.cs`](../../../src/Bookify.Services.Booking.Api/Idempotency/IdempotencyMiddleware.cs) — HTTP header, hash, replay and sensitive-response handling.
- [`IdempotencyProcessor.cs`](../../../src/Bookify.Services.Booking.Application/Idempotency/IdempotencyProcessor.cs) — claim and 24-hour retention.
- [`EfCoreIdempotencyStore.cs`](../../../src/Bookify.Services.Booking.Infrastructure/Persistence/Idempotency/EfCoreIdempotencyStore.cs) — persistent caller-scoped uniqueness.
- [`InitiatePaymentCommandHandler.cs`](../../../src/Bookify.Services.Booking.Application/Payments/Initiate/InitiatePaymentCommandHandler.cs) — durable provider operation reservation.
- [`CreateBookingEndpoint.cs`](../../../src/Bookify.Services.Booking.Api/Endpoints/Bookings/Create/CreateBookingEndpoint.cs) — guest token issued in initial response.
