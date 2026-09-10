# ADR 0001 — Modular monolith behind a gateway

- **Status:** Accepted
- **Date:** 2026-09-10

## Context

Dorado Cloud must run as a single self-hostable unit (one `docker compose up`)
**and** scale to an official multi-tenant instance. It needs clear domain
boundaries so features can be developed independently and, if ever necessary,
extracted into separate services.

## Decision

Build a **modular monolith**: one ASP.NET Core host (`DoradoCloud.Api`) that
discovers and maps every `IEndpointModule` in `DoradoCloud.Modules` under
`/v1/{module}`. A thin YARP gateway (`DoradoCloud.Gateway`) fronts the API for
TLS, routing and edge concerns. Modules communicate in-process today.

## Consequences

- One deployable artifact for self-hosters; low operational burden.
- Module boundaries (`identity`, `catalog`, `artwork`, `directory`, `recs`,
  `social`, `updates`, `media`) are explicit and route-stable.
- Extraction into microservices later requires no public API change — the
  gateway routing table is the seam.
- A shared database is used initially; modules must not reach into each other's
  tables (read via contracts/services), to keep extraction viable.
