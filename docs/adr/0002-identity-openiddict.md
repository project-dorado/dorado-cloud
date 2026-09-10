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

M0 auto-approves the authorize endpoint (demo principal) so the flow is
end-to-end testable; **M1 replaces this with a real login/consent screen** and
moves the dev certificates to real signing/encryption keys.

## Consequences

- Standards-based auth interoperates with desktop and Android libraries.
- No dependency on an external IdP; self-hosters run a complete stack.
- M0 simplifies trust (auto-approval, development certificates) and MUST be
  hardened before any public instance: real login, persisted keys, consent,
  account lifecycle (GDPR export/delete).
