# Bookify — Endpoint Access Matrix

## Purpose

This document defines the access classification of the Booking API.

Authentication identifies a caller.

Authorization decides whether that caller may perform a particular
operation on a particular resource.

Resource-level policies are implemented in module 17.6.

## Public endpoints

These endpoints intentionally allow anonymous access.

- GET /api/v1/properties
- GET /api/v1/properties/{propertyId}
- GET /api/v1/properties/{propertyId}/units
- GET /api/v1/properties/{propertyId}/availability
- POST /api/v1/bookings
- GET /health

POST /api/v1/bookings may also receive a valid Bearer token.

When the caller is authenticated, the Booking is associated with the
authenticated subject through Booking.CustomerSubjectId.

Otherwise guest checkout is used.

## Customer or guest booking access

These operations belong to the customer who owns the Booking or to the
guest holding its opaque access credential.

- GET /api/v1/bookings/{identifier}
- POST /api/v1/bookings/{bookingId}/cancel
- POST /api/v1/payments
- GET /api/v1/payments/bookings/{bookingId}/status

Authenticated access uses the Bearer token subject.

Guest access uses:

Booking-Guest-Token

The raw guest credential is never stored.

Module 17.5 classifies these routes.

Module 17.6 enforces Booking ownership and guest credential validation
through authorization policies.

## Owner or Admin endpoints

Existing Owner/Admin operations:

- POST /api/v1/properties
- POST /api/v1/bookings/{bookingId}/approve
- POST /api/v1/bookings/{bookingId}/reject

These endpoints require an Owner or Admin role.

For Booking actions, role membership alone does not prove ownership.

Module 17.6 adds resource ownership authorization.

Property management, RentableUnit management, pricing management and
calendar APIs that do not exist yet are not introduced by module 17.5.

Their implementation remains assigned to module 21.

## System-only operations

The following operations are not public HTTP endpoints:

- mark Booking as paid
- expire Booking payment
- complete Booking

Payment success is driven by authoritative payment reconciliation.

Payment expiration and Booking completion are internal application
operations that will be invoked by backend processes.

No external client, Owner, Customer or Admin receives direct HTTP
access to these commands.

## Provider webhook

Stripe webhook:

- POST /api/v1/payments/webhooks/stripe

The webhook does not use Bookify user JWT authentication.

Its authenticity is established through the Stripe-Signature header
and provider signature validation.

## Authorization roadmap

17.5:
- endpoint classification;
- explicit public routes;
- Owner/Admin role boundary;
- removal of system-only HTTP routes;
- provider webhook classification.

17.6:
- AdminOnly;
- PropertyOwnerOrAdmin;
- BookingOwner;
- BookingGuestAccess;
- CustomerOrGuestBookingAccess.

17.9:
- complete authorization matrix tests.
