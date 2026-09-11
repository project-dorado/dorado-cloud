# ADR 0002 — Identity via OpenIddict (OIDC)

- **Status:** Accepted
- **Date:** 2026-09-10

## Context

Accounts are optional and cloud is strictly additive: Dorado and Dorado-HD must
work fully offline. When users opt in, they need a standard way to sign in on a
desktop app and on Android, and services need to authenticate each other.

## Decision

Use **OpenIddict 5** as an embedded OAuth 2.0 / OpenID Connect server with EF
Core persistence and local token validation.

- **Public clients** (`dorado-desktop`, `dorado-hd`) use **authorization code +
  PKCE** with refresh tokens.
- A **confidential client** (`dorado-cloud-smoke`) uses **client credentials**
  for service-to-service and CI.
- Tokens are validated locally (`UseLocalServer()`); other modules call
  `.RequireAuthorization()`.

The authorize endpoint now requires an authenticated cookie session and
**challenges to the real `/account/login` page** (M1); development certificates
were replaced by persisted signing/encryption keys in M1. The **consent screen
remains pending** (the seeded public clients still use implicit consent).

## Consequences

- Standards-based auth interoperates with desktop and Android libraries.
- No dependency on an external IdP; self-hosters run a complete stack.
- **Hardened in M1:** real login/register/logout, persisted signing/encryption
  keys, device registry, versioned settings sync, and GDPR export + erasure.
- **Hardened in the identity pass:** per-IP auth rate limiting, a fail-closed
  admin policy outside development, a CORS allowlist, and a development-only
  smoke client.
- **Still required before a public instance:** a consent screen, CSRF/antiforgery
  on the HTML forms, email verification, and password reset.
