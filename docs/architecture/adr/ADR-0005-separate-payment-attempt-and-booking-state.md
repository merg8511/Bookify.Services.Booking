# ADR-0005: Separate Booking, Payment and PaymentAttempt state and reconcile provider events

> **Status:** Accepted — retrospective documentation of the implemented architecture  
> **Recorded:** 2026-11-04 · **Scope:** Bookify.Services.Booking backend · **Module:** 24.7  
> **Decision date:** Not independently established. The recorded date documents the decision; it is not claimed to be the original adoption date.

## Context

A booking can be approved yet unpaid; a payment can be pending or fail without cancelling a reservation immediately; and one payment can have more than one historical attempt. Stripe is an external system that responds asynchronously. Directly copying PaymentIntent status into Booking status would confuse business state with provider state.

## Decision

Maintain separate **Booking**, **Payment** and **PaymentAttempt** models. A new Booking starts `PendingApproval`; approval moves it to `PendingPayment`. A Payment tracks its attempts and current status, and a PaymentAttempt stores the external reference, amount, status and operation key. Booking moves to `Paid` only through validated reconciliation of a succeeded payment, with state guards.

Stripe initiation creates a PaymentIntent with `bookify_booking_id` metadata. The webhook validates `Stripe-Signature` and matches the event ID, booking metadata and external PaymentAttempt reference before invoking `PaymentReconciler`. Unknown/unrelated events do not justify a Booking transition.

## Alternatives considered (retrospective)

The following are trade-off analyses, **not** a claim that a historical design meeting formally voted on every option.

| Alternative | Trade-off for Bookify |
| --- | --- |
| Store Stripe status directly on Booking | Simple schema but loses retries, ownership of attempts and independent payment history. |
| Treat payment initiation as success | Incorrect when a PaymentIntent is still pending or requires user action. |
| Unconditionally apply every webhook | Allows out-of-order, duplicate or unrelated events to corrupt state. |

## Consequences and trade-offs

**Benefits:** auditable payment history, recoverable initiations, separate provider and domain invariants. **Costs:** more entities, coordinated transactions and explicit status mapping. **Current mappings:** `payment_intent.succeeded` → Succeeded; `payment_intent.payment_failed` → Failed; `payment_intent.canceled` → Cancelled. Stripe events are processed only with valid cryptographic signatures. **Guard:** a cancelled or not-yet-approved booking cannot be casually marked Paid.

## Verification and review triggers

- Exercise provider success/failure/cancellation, duplicates and webhook/expiration races.
- Confirm initiation is distinguishable from settlement in the API and Postman docs.
- Reevaluate the model if refunds or partial payments become documented product requirements; do not assume they exist today.
- Regress the webhook security contract in 24.8.

## Source of truth

- [`Booking.cs`](../../../src/Bookify.Services.Booking.Domain/Bookings/Booking.cs) — Booking lifecycle rules.
- [`Payment.cs`](../../../src/Bookify.Services.Booking.Domain/Payments/Payment.cs) — payment aggregate and history.
- [`PaymentAttempt.cs`](../../../src/Bookify.Services.Booking.Domain/Payments/PaymentAttempt.cs) — attempt identity and transitions.
- [`PaymentReconciler.cs`](../../../src/Bookify.Services.Booking.Application/Payments/Reconciliation/PaymentReconciler.cs) — domain-safe reconciliation.
- [`ProcessStripeWebhookCommandHandler.cs`](../../../src/Bookify.Services.Booking.Application/Payments/Webhooks/Stripe/ProcessStripeWebhookCommandHandler.cs) — verified webhook and event deduplication.
- [`StripePaymentGateway.cs`](../../../src/Bookify.Services.Booking.Infrastructure/Payments/Stripe/StripePaymentGateway.cs) — Stripe metadata and PaymentIntent mapping.
