# Dorado Cloud

Community cloud services for the [Dorado](https://github.com/project-dorado/dorado)
desktop player and [Dorado-HD](https://github.com/project-dorado/dorado-hd) Android client.

Dorado Cloud replaces the long-dead Zune web services with a modern, open,
self-hostable backend: shared catalog and artwork, identity and cross-device
settings, recommendations, social features, podcast/radio directories, signed
app updates, and DRM-free (public-domain / Creative Commons) streaming.

> **Not affiliated with Microsoft.** Dorado is an independent homage. Dorado Cloud
> does not contact or emulate Microsoft Zune services and hosts no copyrighted
> media. See [`NOTICE.md`](NOTICE.md).

## Status

| Milestone | Scope | State |
|---|---|---|
| **M0 Foundations** | modular monolith, gateway, OpenIddict identity, Postgres/Redis/MinIO, compose + Helm, CI→ghcr, OpenAPI, client SDK | ✅ |
| **M1 Identity + sync + updates** | real accounts + login, persistent keys, device registry, settings sync, signed appcast | ✅ |
| M2 Directory | podcast + radio search | ✅ |
| M3 Catalog + artwork | MusicBrainz/CAA + artwork CDN (object storage) | ✅ |
| M4 Social | profiles, activity, Zune Card, badges | ✅ |
| M5 QuickMix | similarity recommendations | ✅ (heuristic + embedding blend; pgvector opt-in) |
| M6 Media | PD/CC streaming (after legal review) | ⏳ model + ingestion shipped behind `Media:Enabled=false`; enablement legal-gated |
| M7 Legacy Zune compat | `*.zune.net` Atom/XML hosts for hosts-patched clients | ✅ (phases 0–4) |
| M8 Legacy client enablement | WS-Trust session bridge, hosts/TLS runbook | ✅ |
| M9 Catalog completeness | artist imagery, podcast hub, empty video hubs | ✅ |
| M10 Embeddings | similarity store + opt-in pgvector | ✅ |
| M11 Identity hardening | antiforgery, consent, email verify, password reset | ✅ |
| M12 Providers & ops | Fanart.tv/TheAudioDB/Discogs, runbooks, dashboard | ✅ |

## Architecture

A **modular monolith** (`DoradoCloud.Api`) where each domain is an
`IEndpointModule` mounted under `/v1/{module}`, behind a thin YARP gateway.
Modules can be extracted into independent services later without changing the
public routes.

```
client (Dorado / Dorado-HD)
        │  HTTPS
        ▼
DoradoCloud.Gateway  (YARP)
        │
        ▼
DoradoCloud.Api
 ├─ identity        OpenIddict (OIDC) + EF Core accounts
 ├─ catalog         artists / releases / recordings        (MusicBrainz)
 ├─ artwork         proxy + cache + resize                 (object storage CDN)
 ├─ directory       podcasts + radio                       (Podcast Index / Radio-Browser)
 ├─ recs            QuickMix recommendations               (heuristic; pgvector later)
 ├─ social          profiles / activity / Zune Card        (M4)
 ├─ updates         signed app manifests                   (RS256)
 └─ media           PD/CC streaming only                   (gated → M6)
```

Alongside the modern JSON API, **legacy Zune compatibility modules** recreate
the original `*.zune.net` hosts (Atom/XML) at the root, dispatched by the
request `Host` header (see [`docs/adr/0004`](docs/adr/0004-legacy-zune-compat-layer.md)).

- **Runtime:** .NET 8, ASP.NET Core minimal APIs
- **Auth:** OpenIddict 5 (authorization code + PKCE, refresh, client credentials)
- **Data:** PostgreSQL + EF Core (SQLite for local/dev), Redis cache, S3/MinIO object storage
- **Observability:** OpenTelemetry (opt-in OTLP), structured logging, health probes
- **Contract:** OpenAPI at `/swagger`, spec at `/swagger/v1/swagger.json`

## Quickstart

### Run locally (no containers)

```bash
dotnet restore DoradoCloud.sln
dotnet run --project src/DoradoCloud.Api       # http://localhost:5080
```

SQLite is used by default; Swagger UI is at http://localhost:5080/swagger.
Discovery is published at `/.well-known/openid-configuration`.

> **Schema.** With PostgreSQL the host applies EF Core **migrations** at startup
> (`src/DoradoCloud.Modules/Data/Migrations`, including the OpenIddict tables);
> local development uses SQLite with `EnsureCreated`. Author migrations with the
> repo-local tool: `dotnet tool restore`, then
> `dotnet ef migrations add <Name> --project src/DoradoCloud.Modules --startup-project src/DoradoCloud.Api`.

### Self-host the full stack

```bash
cp .env.example .env      # set POSTGRES_PASSWORD and MinIO credentials
docker compose up --build
```

The gateway then serves on http://localhost:5088 (API on :5080, MinIO console on :9001).

### Kubernetes

```bash
helm install dorado-cloud deploy/helm/dorado-cloud \
  --set postgres.password='…' --set minio.accessKey='…' --set minio.secretKey='…' \
  --set auth.issuer='https://cloud.example/'
```

## Identity, sync & updates (M1)

- **Accounts** — email + password (PBKDF2 via `PasswordHasher`), browser login at
  `/account/login` / `/account/register`, cookie session (`.dorado.sid`).
- **OIDC** — authorization-code + PKCE for `dorado-desktop` / `dorado-hd`,
  refresh tokens, client-credentials for services. Persistent signing/encryption
  keys (see `Auth:Certificates:*`); development certificates are gated by
  `Auth:UseDevelopmentCertificates`.
- **Device registry** — register/list/remove the devices an account has enrolled.
- **Settings sync** — a per-account JSON document with optimistic concurrency
  (send the version you last saw; a stale write returns `409 Conflict`).
- **Signed update feed** — publish releases and serve them with a detached
  RS256 signature; the public key is at `/v1/updates/signing-key` and clients
  verify with `UpdateManifestCrypto` (in `DoradoCloud.Shared`).

| Endpoint | Auth | Purpose |
|---|---|---|
| `POST /account/register`, `POST /account/login`, `POST /account/logout` | cookie | interactive account lifecycle |
| `GET /connect/authorize`, `POST /connect/token` | OIDC | authorization-code + PKCE, refresh, client credentials |
| `GET /v1/identity/me` | bearer | current account/service principal |
| `GET/POST /v1/identity/me/devices`, `DELETE …/{id}` | bearer (account) | device registry |
| `GET/PUT /v1/identity/me/settings` | bearer (account) | settings sync (versioned) |
| `GET /v1/identity/me/export` | bearer (account) | GDPR data-portability export |
| `DELETE /v1/identity/me` | bearer (account) | GDPR erasure (deletes account + data, revokes tokens) |
| `GET /v1/updates/{app}/{channel}`, `GET /v1/updates/signing-key` | public | signed update feed + verification key |
| `POST /v1/updates/publish` | bearer (`UpdatesAdmin`) | publish a signed release |

## Directory — podcasts & radio (M2)

Thin, cached adapters over open providers:

- **Podcasts** via **Podcast Index** (requires an API key/secret; searches are
  signed per request). Unconfigured ⇒ a well-formed empty response, not an error.
- **Radio** via **Radio-Browser** (no key; can be disabled).

Responses carry the provider **attribution**; results are cached in the
distributed cache (Redis in the compose stack, in-process otherwise).

| Endpoint | Purpose |
|---|---|
| `GET /v1/directory/podcasts/search?q=&limit=` | search podcasts by term |
| `GET /v1/directory/radio/search?q=&country=&tag=&limit=` | search radio stations |

## Catalog & artwork (M3)

- **Catalog** — `MusicBrainzClient` searches artists, release-groups and
  recordings, and looks up artists by MBID. Calls are **rate-limited per host**
  (MusicBrainz ~1 req/s) and cached; every response carries attribution and
  release-groups include their Cover Art Archive URL.
- **Artwork CDN** — `ArtworkService` fetches front covers by release-group MBID
  (or an allowlisted provider URL), caches the bytes **content-addressed in
  object storage**, and serves them with a long-lived `Cache-Control`. Arbitrary
  proxy URLs are restricted to an **allowlist of provider hosts** (SSRF guard).
- **Object storage** — local filesystem by default, or **S3/MinIO** via
  `Storage:Provider=s3`.

| Endpoint | Purpose |
|---|---|
| `GET /v1/catalog/search?q=&type=artist\|release-group\|recording&limit=` | search the catalog |
| `GET /v1/catalog/artists/{mbid}` | artist lookup |
| `GET /v1/artwork/front/{releaseGroupMbid}?size=250\|500\|1200` | front cover (cached) |
| `GET /v1/artwork/proxy?url=` | allowlisted provider imagery (cached) |

## Social (M4)

- **Profiles** with unique handles; **follow graph** (follow/unfollow, followers/following).
- **Activity feed** — post activities (`now-playing`, `rated`, …); the feed shows
  your own and followed accounts' activity, newest first, and **excludes blocked
  accounts in both directions**.
- **Zune Card** — followers/following/activity counts, earned badges and recent
  activity.
- **Badges** — a static catalog (`first-post`, `connector`, …) auto-granted on
  milestones or via `POST /me/badges/{code}`.
- **Moderation** — block/unblock, file reports, and an admin queue
  (`Admin` policy) to list and resolve them.

| Endpoint | Auth | Purpose |
|---|---|---|
| `GET /v1/social/badges` | public | badge catalog |
| `GET /v1/social/profiles/{handle}` | public | profile (+ counts, viewer state) |
| `GET /v1/social/profiles/{handle}/followers`, `/following` | public | graph |
| `GET /v1/social/profiles/{handle}/zunecard` | public | Zune Card |
| `PUT/GET /v1/social/profiles/me` | bearer | create/update or read own profile |
| `POST/DELETE /v1/social/profiles/{handle}/follow` | bearer | follow / unfollow |
| `GET /v1/social/me/feed`, `POST /v1/social/me/activities` | bearer | feed & posting |
| `POST /v1/social/me/badges/{code}` | bearer | grant a badge |
| `POST/DELETE /v1/social/profiles/{handle}/block`, `GET /v1/social/me/blocks` | bearer | blocking |
| `POST /v1/social/reports` | bearer | file a report |
| `GET /v1/social/admin/reports`, `POST …/{id}/resolve` | bearer (`Admin`) | moderation queue |

## QuickMix (M5)

`GET /v1/recs/quickmix?seed=&limit=` (and `POST /v1/recs/quickmix`) turns an artist name **or** MBID into scored,
explained recommendations. The pragmatic engine:

1. resolves the seed (MusicBrainz search when a name is given),
2. scores **related artists** (band members / collaborators) and artists that
   **share the seed's top genre tags**, and
3. returns ranked candidates with a score and human-readable reasons.

It is deterministic, cached and offline-testable. The endpoint contract is stable,
so a **pgvector embedding** pipeline (ListenBrainz/AcousticBrainz) can replace the
scoring later without client changes.

## Legacy Zune compatibility (M7)

Hosts-patched Zune 4.8 desktop software and Zune HD devices speak the original
Atom/XML services to a set of host names. Dorado Cloud recreates them as
host-routed modules (no `/v1` prefix); each answers only on its declared `Host`.
See [ADR 0004](docs/adr/0004-legacy-zune-compat-layer.md).

| Host | Service | Backing |
|---|---|---|
| `catalog.zune.net` | hubs, genres, albums, artists, tracks, charts, `v4.0` similarTracks, app catalog | MusicBrainz + Cover Art Archive; app packages from an external corpus |
| `image.catalog.zune.net` | cover art | artwork CDN |
| `resources.zune.net` | `FirmwareUpdate.xml`, `v4_5/zuneprod.xml`, baseline CABs | external firmware corpus |
| `mix.zune.net` | Mixview similar tracks | shared catalog |
| `socialapi.zune.net` | members, friends, badges | social graph |
| `inbox.zune.net` | messaging | `InboxMessage` store |
| `tiles.zune.net` | member backgrounds/avatars (read-only) | external tile corpus |
| `tuners.zune.net` | PC-client resources | external corpus |
| `login.zune.net` | WS-Trust `RST2.srf` login | account store, **gated** |
| `fai.music.metaservices.microsoft.com` | `ZuneAPI/EndPoints.aspx` | discovery document |

**Legal posture.** No Microsoft firmware, artwork or packages are committed:
CABs, `.zcp` packages and PC-client resources stream from a configured,
untracked corpus root and fail closed (`404`) when unset. `commerce.zune.net`
purchase and Zune-Pass DRM/license endpoints are **not** implemented (no DRM
circumvention). The WS-Trust login bridge is disabled by default (`501`); once
enabled it issues a `Compact1` ticket the client re-sends as
`Authorization: WLID1.0 <ticket>`, resolved to an account by the hash-only
`LegacySession` store.

Client setup, TLS and corpora are documented in
[docs/legacy-client-setup.md](docs/legacy-client-setup.md) (with a sample
reverse proxy in [`deploy/legacy/`](deploy/legacy/) and a smoke script at
[`tools/legacy-smoke.sh`](tools/legacy-smoke.sh)).

Operations runbooks: [backup/restore](docs/ops/backup-restore.md),
[metrics](docs/ops/metrics.md), [moderation](docs/ops/moderation.md).

## Calling the API

```bash
# client-credentials token (service-to-service)
curl -s -X POST http://localhost:5080/connect/token \
  -d 'grant_type=client_credentials' -d 'client_id=dorado-cloud-smoke' \
  -d 'client_secret=dev-secret' -d 'scope=dorado.api'

# module liveness
curl http://localhost:5080/v1/catalog/ping
```

The typed SDK lives in [`clients/DoradoCloud.Client`](clients/DoradoCloud.Client):

```csharp
services.AddDoradoCloud(new Uri("https://cloud.dorado.example/"));
// Optional: attach bearer tokens and refresh them automatically (OIDC refresh_token).
services.AddDoradoCloudAuth(
    new Uri("https://cloud.dorado.example/"),
    clientId: "dorado-desktop",
    credentialStoreFactory: sp => sp.GetRequiredService<ICloudCredentialStore>());
```

`AddDoradoCloudAuth` installs a `DelegatingHandler` that reads the credential
from your `ICloudCredentialStore`, attaches the bearer token, refreshes it when
expired, and replays once after a `401`. Interactive sign-in stays with the host
(desktop loopback PKCE, Android Custom Tabs); the SDK owns token lifetime.

## Configuration

| Key | Purpose | Default |
|---|---|---|
| `ConnectionStrings:Postgres` | PostgreSQL DSN; empty ⇒ SQLite | _(empty)_ |
| `Auth:SqlitePath` | SQLite file when no Postgres | `dorado-cloud-auth.db` |
| `Auth:Issuer` | Public issuer URL advertised by OpenIddict | `http://localhost:5080/` |
| `Auth:DisableTransportSecurity` | Allow non-TLS endpoints (dev only) | `false` |
| `Auth:UseDevelopmentCertificates` | Use ephemeral dev certificates instead of persisted keys | `false` (`true` in Development) |
| `Auth:Certificates:Path` / `:Password` | Directory for `openiddict-signing.pfx` / `openiddict-encryption.pfx` | `data/keys` |
| `Updates:SigningKeyPath` | RSA PKCS#8 PEM used to sign update manifests | `data/keys/updates-signing.pem` |
| `Auth:RateLimit:PermitPerMinute` | Auth endpoint rate limit (per IP) on `/connect/token`, `/account/login`, `/account/register` | `60` |
| `Cors:AllowedOrigins` | Browser CORS allowlist (empty ⇒ permissive in Development, same-origin otherwise) | `[]` |
| `Updates:Admins` | Subjects/emails allowed to publish releases (empty ⇒ no admin rights outside Development) | `[]` |
| `Directory:CacheSeconds` | TTL for cached directory responses | `300` |
| `Directory:PodcastIndex:ApiKey` / `:ApiSecret` | Podcast Index credentials (empty ⇒ directory degrades to empty) | _(empty)_ |
| `Directory:RadioBrowser:Enabled` | Enable the Radio-Browser adapter | `true` |
| `Resources:CorpusRoot` | External firmware baseline CAB directory (empty ⇒ `resources.zune.net` disabled) | _(empty)_ |
| `Resources:PublicBaseUrl` | Base URL used for CAB links in the firmware manifest | `http://resources.zune.net` |
| `Tiles:CorpusRoot` | External tile corpus (`Background/`, `Avatar/`); empty ⇒ disabled | _(empty)_ |
| `Apps:CorpusRoot` | External `.zcp` app corpus for the read-only catalog | _(empty)_ |
| `Tuners:CorpusRoot` | External PC-client resource corpus; empty ⇒ disabled | _(empty)_ |
| `Legacy:Login:Enabled` | Enable the gated `login.zune.net` WS-Trust login bridge | `false` |
| `Legacy:Login:PublicBaseUrl` | Host advertised in `login.zune.net/ppcrlconfig.bin` | `https://login.zune.net` |
| `Legacy:Session:TtlHours` | Lifetime of an issued legacy session ticket | `720` |
| `Identity:Security:RequireEmailVerification` | Require a verified email to sign in | `false` |
| `Identity:Security:PublicBaseUrl` | Base URL for emailed verification/reset links | `http://localhost:5080` |
| `Media:Enabled` | Enable PD/CC media streaming/ingestion (legal-gated) | `false` |
| `Media:AllowedLicenses` | Accepted licenses for ingestion | `PD, CC0, CC-BY, CC-BY-SA, …` |
| `Embedding:Enabled` | Enable embedding ingestion (admin endpoint) | `false` |
| `Embedding:UsePgvector` | Use the pgvector `<=>` path (see `deploy/pgvector`) | `false` |
| `Providers:FanartTv:ApiKey` / `:TheAudioDb:ApiKey` / `:Discogs:Token` | Optional artist-image enrichment (empty ⇒ disabled) | _(empty)_ |
| `Storage:Provider` | `local` or `s3` (MinIO/AWS) | `local` |
| `Storage:S3:ServiceUrl` / `:Bucket` / `:AccessKey` / `:SecretKey` | S3-compatible endpoint + credentials | _(empty)_ |
| `Catalog:MusicBrainzRateLimitMs` | Minimum spacing between MusicBrainz calls | `1000` |
| `Artwork:AllowedHosts` | Hosts the artwork proxy may fetch (SSRF guard) | provider allowlist |
| `Admin:Subjects` | Subjects/emails allowed to moderate + publish (empty ⇒ no admin rights outside Development) | `[]` |
| `Redis:Configuration` | StackExchange.Redis connection | _(empty)_ |
| `Otel:Endpoint` | OTLP collector endpoint (enables tracing/metrics) | _(empty)_ |

## Repository layout

```
src/DoradoCloud.Api        host + module discovery + OpenAPI
src/DoradoCloud.Modules    modules + Identity (OpenIddict)
src/DoradoCloud.Legacy     Atom/XML writer + legacy id mapping
src/DoradoCloud.Gateway    YARP edge proxy
src/DoradoCloud.Shared     contracts shared by host, modules and SDK
clients/DoradoCloud.Client typed HTTP client SDK
tests/DoradoCloud.Tests    integration tests (WebApplicationFactory)
docker-compose.yml + deploy/helm   self-host & official-instance deployment
docs/adr                   architecture decision records
```

## Legal & licensing

Code is MIT. Dorado Cloud hosts no copyrighted media and does not reimplement
DRM. Metadata/artwork come from openly licensed providers with attribution; see
[`NOTICE.md`](NOTICE.md). Contributions welcome — see the organization
[`CONTRIBUTING.md`](https://github.com/project-dorado/.github/blob/main/CONTRIBUTING.md).
