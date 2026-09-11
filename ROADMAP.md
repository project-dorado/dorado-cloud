# Dorado Cloud — Roadmap & Status

Backend services for [Dorado](https://github.com/project-dorado/dorado) (desktop)
and [Dorado-HD](https://github.com/project-dorado/dorado-hd) (Android).

**Last updated:** 2026-09-10 · **HEAD:** `13fb09f` · **Tests:** 61/61 (44 integration + 17 client) · **Build:** 0 warnings (`/warnaserror`) · **CI:** green (build+test, api & gateway images → ghcr)

Legend: ✅ done · 🚧 in progress · ⏳ pending

## Milestones

| # | Milestone | State | Summary |
|---|---|---|---|
| M0 | Foundations | ✅ | .NET 8 modular monolith + YARP gateway, OpenIddict, Postgres/Redis/MinIO, compose + Helm, CI→ghcr, OpenAPI, client SDK |
| M1 | Identity + sync + updates | ✅ | Accounts + browser login, persistent OIDC keys, device registry, versioned settings sync, RS256-signed update feed |
| M2 | Directory | ✅ | Podcast Index + Radio-Browser adapters, caching, attribution, graceful degradation |
| M3 | Catalog + artwork CDN | ✅ | MusicBrainz (rate-limited) catalog, artwork proxy/cache/resize in object storage, SSRF allowlist |
| M4 | Social | ✅ | Profiles, follow graph, activity feed, Zune Card, badges, moderation (block/report/admin) |
| M5 | QuickMix | ✅ | Heuristic similarity (MusicBrainz relationships + genre tags), scored + explained |
| M6 | Media (PD/CC streaming) | ⏳ | **Legal-gated** — public-domain / Creative Commons only, after legal review |

## What's done

- **8 modules** mounted at `/v1/{module}`: `identity`, `catalog`, `artwork`, `directory`, `recs`, `social`, `updates`, `media`.
- **Identity**: OIDC (auth-code+PKCE, refresh, client-credentials), account register/login/logout, persistent signing/encryption keys, `Admin` policy.
- **Sync**: device registry + per-account settings with optimistic concurrency.
- **Updates**: publish + serve signed manifests; public verification key; `UpdateManifestCrypto` shared with clients.
- **Catalog/artwork**: MusicBrainz search/lookup, Cover Art Archive, allowlisted artwork proxy cached in object storage (local FS or S3/MinIO).
- **Social**: profiles, follow, feed (blocked-excluded), Zune Card, badges, moderation queue.
- **Recommendations**: `GET/POST /v1/recs/quickmix`.
- **Ops**: Dockerfiles, `docker-compose` (Postgres/Redis/MinIO), Helm chart, OpenTelemetry (opt-in OTLP), health probes, Swagger.

## What's pending

| Item | Priority | Notes |
|---|---|---|
| **M6 — Media (PD/CC only)** | P1 (legal) | License-gated catalog + streaming from Internet Archive / Wikimedia; requires legal sign-off. No copyrighted media. |
| **Client integration** | P1 | ✅ SDK centralized auth (`DoradoCloudAuthHandler`, `AddDoradoCloudAuth`); desktop + HD update-check live. Remaining: interactive browser-PKCE E2E; wire any remaining desktop surfaces (`IMixviewService`, `ICloudUpdateService` apply path). |
| **pgvector QuickMix upgrade** | P2 | Replace heuristic scoring with embeddings (ListenBrainz/AcousticBrainz); endpoint contract already stable. |
| **EF Core migrations** | P2 | Replace `EnsureCreated` for production schema evolution. |
| **Identity hardening** | P2 | Real consent screen, login rate-limiting, CSRF on HTML forms, account lifecycle (verification, password reset, GDPR export/delete). |
| **Native providers** | P2 | Discogs / TheAudioDB / Fanart.tv enrichment beyond MusicBrainz + CAA. |
| **Ops** | P3 | Redis cache tuning, backup/restore runbook, metrics dashboards, moderation ops docs. |

## Repository layout

```
src/DoradoCloud.Api        host + module discovery + OpenAPI
src/DoradoCloud.Modules    modules (identity, catalog, artwork, directory, recs, social, updates, media) + storage + providers
src/DoradoCloud.Gateway    YARP edge proxy
src/DoradoCloud.Shared     contracts shared by host, modules and SDK
clients/DoradoCloud.Client typed HTTP client SDK
tests/DoradoCloud.Tests    44 integration/unit tests
deploy/helm                Helm chart
docs/adr                   architecture decision records
```

## Run it

```bash
# local
dotnet run --project src/DoradoCloud.Api          # http://localhost:5080  (Swagger at /swagger)

# full stack (Postgres + Redis + MinIO)
cp .env.example .env && docker compose up --build # gateway on :5088
```
