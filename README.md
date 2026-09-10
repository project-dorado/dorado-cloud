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

## Status — M0 (foundations)

| Milestone | Scope | State |
|---|---|---|
| **M0 Foundations** | modular monolith, gateway, OpenIddict identity, Postgres/Redis/MinIO, compose + Helm, CI→ghcr, OpenAPI, client SDK | ✅ |
| **M1 Identity + sync + updates** | real accounts + login, persistent keys, device registry, settings sync, signed appcast | ✅ |
| M2 Directory | podcast + radio search | ⏳ |
| M3 Catalog + artwork | MusicBrainz/CAA/Discogs + art CDN | ⏳ |
| M4 Social | profiles, activity, Zune Card, badges | ⏳ |
| M5 QuickMix | recommendations pipeline | ⏳ |
| M6 Media | PD/CC streaming (after legal review) | ⏳ |

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
 ├─ catalog         artists / releases / recordings        (stub → M3)
 ├─ artwork         proxy + cache + resize                 (stub → M3)
 ├─ directory       podcasts + radio                       (stub → M2)
 ├─ recs            QuickMix recommendations               (stub → M5)
 ├─ social          profiles / activity / Zune Card        (stub → M4)
 ├─ updates         signed app manifests                   (stub → M1)
 └─ media           PD/CC streaming only                   (gated → M6)
```

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
| `GET /v1/updates/{app}/{channel}`, `GET /v1/updates/signing-key` | public | signed update feed + verification key |
| `POST /v1/updates/publish` | bearer (`UpdatesAdmin`) | publish a signed release |

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
```

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
| `Updates:Admins` | Subjects/emails allowed to publish releases (empty ⇒ any authenticated) | `[]` |
| `Redis:Configuration` | StackExchange.Redis connection | _(empty)_ |
| `Storage:S3:*` | S3/MinIO endpoint, bucket, credentials | _(empty)_ |
| `Otel:Endpoint` | OTLP collector endpoint (enables tracing/metrics) | _(empty)_ |

## Repository layout

```
src/DoradoCloud.Api        host + module discovery + OpenAPI
src/DoradoCloud.Modules    modules + Identity (OpenIddict)
src/DoradoCloud.Gateway    YARP edge proxy
src/DoradoCloud.Shared     contracts shared by host, modules and SDK
clients/DoradoCloud.Client typed HTTP client SDK
tests/DoradoCloud.Tests    integration tests (WebApplicationFactory)
deploy/compose + helm      self-host & official-instance deployment
docs/adr                   architecture decision records
```

## Legal & licensing

Code is MIT. Dorado Cloud hosts no copyrighted media and does not reimplement
DRM. Metadata/artwork come from openly licensed providers with attribution; see
[`NOTICE.md`](NOTICE.md). Contributions welcome — see the organization
[`CONTRIBUTING.md`](https://github.com/project-dorado/.github/blob/main/CONTRIBUTING.md).
