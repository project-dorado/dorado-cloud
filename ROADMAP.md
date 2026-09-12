# Dorado Cloud — Roadmap & Status

Backend services for [Dorado](https://github.com/project-dorado/dorado) (desktop)
and [Dorado-HD](https://github.com/project-dorado/dorado-hd) (Android).

**Last updated:** 2026-09-11 · **HEAD:** `main` · **Tests:** 195/195 (178 integration + 17 client) · **Build:** 0 warnings (`/warnaserror`) · **CI:** green (build+test, api & gateway images → ghcr)

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
| M7 | Legacy Zune compat | ✅ | Host-routed `*.zune.net` Atom/XML services for hosts-patched Zune 4.8 / Zune HD clients (phases 0–4) |
| M8 | Legacy client enablement | ✅ | WS-Trust `Compact1` ticket bridge + hash-only session store, hosts/TLS runbook, design-time DbContext factory |
| M9 | Catalog completeness | ✅ | Artist imagery (Wikidata/Wikimedia + providers), podcast hub, empty video hubs |
| M10 | Embeddings | ✅ | Similarity store + cosine ranking with opt-in pgvector acceleration; QuickMix blend |
| M11 | Identity hardening | ✅ | Antiforgery on forms, OIDC consent screen, email verification, password reset |
| M12 | Providers & ops | ✅ | Fanart.tv/TheAudioDB/Discogs adapters; backup/metrics/moderation runbooks + Grafana sample |

## What's done

- **8 modules** mounted at `/v1/{module}`: `identity`, `catalog`, `artwork`, `directory`, `recs`, `social`, `updates`, `media`.
- **Identity**: OIDC (auth-code+PKCE, refresh, client-credentials), account register/login/logout, persistent signing/encryption keys, `Admin` policy.
- **Sync**: device registry + per-account settings with optimistic concurrency.
- **Updates**: publish + serve signed manifests; public verification key; `UpdateManifestCrypto` shared with clients.
- **Catalog/artwork**: MusicBrainz search/lookup, Cover Art Archive, allowlisted artwork proxy cached in object storage (local FS or S3/MinIO).
- **Social**: profiles, follow, feed (blocked-excluded), Zune Card, badges, moderation queue.
- **Recommendations**: `GET/POST /v1/recs/quickmix`.
- **Schema**: versioned EF Core **migrations** (`src/DoradoCloud.Modules/Data/Migrations`, includes the OpenIddict tables); `MigrateAsync` on PostgreSQL, `EnsureCreated` for local SQLite (author with the repo-local `dotnet-ef` tool).
- **Hardening**: auth rate limiting (per-IP, path-partitioned), fail-closed admin outside development, CORS allowlist (`Cors:AllowedOrigins`), dev-only smoke client, GDPR export + erasure (`GET /v1/identity/me/export`, `DELETE /v1/identity/me` — revokes OIDC tokens).
- **Identity hardening (M11)**: antiforgery on every HTML form (missing/invalid tokens → `400`), OIDC consent screen with accept/deny (`error=access_denied` redirect), hashed single-use email-verification/password-reset tokens with TTLs, pluggable `IEmailSender`, and enforced `Identity:Security:RequireEmailVerification`.
- **Client integration**: the client SDK centralizes auth (`DoradoCloudAuthHandler`, `AddDoradoCloudAuth`); the desktop and Android update-checks are live.
- **Ops**: Dockerfiles, `docker-compose` (Postgres/Redis/MinIO), Helm chart, OpenTelemetry (opt-in OTLP), health probes, Swagger, [runbooks](docs/ops/).
- **Legacy Zune compat (M7, phases 0–4)**: `EndpointModuleBase` host dispatch (`RequireHost`) + `DoradoCloud.Legacy` Atom/XML toolkit; `catalog.zune.net` (hubs/genres/albums/artists/tracks/charts + app catalog), `image.catalog.zune.net`, `resources.zune.net` (firmware manifest + baseline CABs), `mix.zune.net`, `socialapi.zune.net`, `inbox.zune.net` (`InboxMessage` store), `tiles.zune.net`, `tuners.zune.net`, `fai.music.metaservices.microsoft.com`, and a gated `login.zune.net` WS-Trust bridge. Corpora are external/untracked and fail closed; no commerce/DRM.

## What's pending

| Item | Priority | Notes |
|---|---|---|
| **M6 — Media (PD/CC only)** | P1 (legal) | License-gated catalog + streaming from Internet Archive / Wikimedia; requires legal sign-off. No copyrighted media. |
| **Client integration (remaining)** | P1 | Interactive browser-PKCE E2E; wire any remaining desktop surfaces (`IMixviewService`, `ICloudUpdateService` apply path). |
| **Embedding scoring replacement** | P2 | Heuristic + embedding blend shipped (pgvector opt-in, in-memory fallback); replacing the heuristic with a full ListenBrainz/AcousticBrainz pipeline remains. |
| **Native providers** | P2 | Discogs / TheAudioDB / Fanart.tv enrichment beyond MusicBrainz + CAA. |
| **Legacy compat follow-ups** | P1 | Interactive E2E against a real hosts-patched Zune 4.8 / Zune HD client; legacy login token trust model (WS-Trust bridge still gated); keyless artist imagery for `image.catalog.zune.net`. |
| **Ops** | P3 | Redis cache tuning, backup/restore runbook, metrics dashboards, moderation ops docs. |

## Repository layout

```
src/DoradoCloud.Api        host + module discovery + OpenAPI
src/DoradoCloud.Modules    modules (modern + legacy) + storage + providers
src/DoradoCloud.Legacy     Atom/XML writer + legacy id mapping
src/DoradoCloud.Gateway    YARP edge proxy
src/DoradoCloud.Shared     contracts shared by host, modules and SDK
clients/DoradoCloud.Client typed HTTP client SDK
tests/DoradoCloud.Tests    46 integration tests
tests/DoradoCloud.Client.Tests  17 client wire-shape/auth tests
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
