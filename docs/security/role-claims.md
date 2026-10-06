
# Bookify — Identity Role Claims

## Purpose

Bookify.Services.Booking consumes access tokens issued by an
external OIDC identity provider.

Bookify does not manage passwords or issue its own user tokens.

## Identity contract

The stable user identity is obtained from:

- Claim: `sub`

The API validates the issuer, audience, signature and token lifetime
before trusting the claims.

## Role contract

The access token must expose Bookify-specific roles through:

- Claim: `roles`
- Format: multivalued string claim / JSON string array

Supported roles:

- `Admin`
- `Owner`
- `Customer`

Role values are case-sensitive.

A user may have more than one role.

Roles are not hierarchical.

An authenticated user without Bookify roles is not automatically
assigned the Customer role.

## Example access token payload

{
  "iss": "https://identity.example.com/realms/bookify",
  "sub": "user-123",
  "aud": "bookify-booking-api",
  "roles": [
    "Owner",
    "Customer"
  ]
}

This is an illustrative payload, not a signed access token.

## Identity provider requirements

The identity provider must:

1. Issue access tokens for the Bookify Booking API.
2. Include the configured API audience.
3. Preserve a stable subject identifier.
4. Emit Bookify-specific roles in the `roles` claim.
5. Assign administrative roles only through trusted identity
   administration procedures.

For Keycloak, configure an appropriate OIDC protocol mapper.

Do not assume that nested realm_access or resource_access claims
are automatically mapped by the Booking API.

## Authorization boundaries

A role does not establish resource ownership.

Owner access to a particular Property or Booking requires
the corresponding persisted ownership relationship.

Guest checkout uses a separate opaque credential mechanism.

Stripe webhooks use provider signature verification, not
user JWT roles.

## Roadmap

17.2 — Role contract and JWT role mapping.
17.3 — Persisted ownership and guest booking credentials.
17.4 — Current User abstraction.
17.5 — Endpoint access matrix.
17.6 — Authorization policies.
17.9 — Authorization integration tests.

The Keycloak demo runtime belongs to module 29.4.
