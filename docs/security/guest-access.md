
# Bookify — Resource ownership and guest access

## Property ownership

A Property must have OwnerSubjectId.

The value is obtained from the authenticated access token's `sub` claim.
Clients cannot specify or override ownership in CreatePropertyRequest.

Property creation requires an authenticated Owner or Admin.

OwnerSubjectId is an opaque identity-provider subject identifier.
It is not a local user ID or an email address.

## Booking ownership

Bookings support two mutually exclusive access models:

1. Authenticated booking:
   - CustomerSubjectId is populated.
   - GuestAccessTokenHash is null.

2. Guest checkout:
   - CustomerSubjectId is null.
   - GuestAccessTokenHash is populated.

The database enforces this invariant.

## Guest credential issuance

A guest credential contains 32 cryptographically random bytes.

It is represented using Base64Url without padding.

The database stores its SHA-256 hash only.

The raw token is returned on the initial successful creation response
in the Booking-Guest-Token HTTP response header.

The token is never included in the booking JSON response or in an
idempotency response body.

On idempotency replay, the original booking response is replayed
without issuing or disclosing the guest token again.

The frontend must capture and protect the token upon initial receipt.

There is no raw-token recovery from the database.

## Credential verification

The booking must first be located by its identifier.

The provided credential is hashed and compared to the stored hash
using a fixed-time comparison.

BookingReference is not proof of ownership or access.

## Pending authorization

17.4: Current User abstraction.
17.5: Endpoint access matrix.
17.6: Resource authorization policies.
17.8: CORS exposure of Booking-Guest-Token and HTTP security.
17.9: Complete authorization integration tests.

The Booking Service must not be considered ready for public deployment
until sensitive booking and payment endpoints enforce the access model.
