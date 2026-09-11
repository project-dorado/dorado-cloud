# Legacy Zune client setup

This guide shows how to point a hosts-patched **Zune 4.8 desktop** client (and a
Zune HD device) at a Dorado Cloud instance. Dorado recreates the original
`*.zune.net` hosts as host-routed Atom/XML services; see
[ADR 0004](adr/0004-legacy-zune-compat-layer.md).

> Dorado is an independent, non-affiliated homage. It does not contact Microsoft
> services, ships no Microsoft firmware/packages/artwork, and implements no
> commerce or DRM endpoints. See [`NOTICE.md`](../NOTICE.md).

## 1. Run Dorado Cloud

```bash
cp .env.example .env          # set Postgres/MinIO secrets
docker compose up --build     # gateway on :5088, API on :5080
```

Legacy endpoints are reachable once the API is running; the gateway must
preserve the original `Host` (it does — `RequestHeaderOriginalHost: true`).

## 2. Route the dead host names

Add these to the machine hosting the client (`%SystemRoot%\System32\drivers\etc\hosts`
on Windows, `/etc/hosts` elsewhere), pointing at the machine running the gateway:

```
127.0.0.1 catalog.zune.net catalog-ssl.zune.net image.catalog.zune.net
127.0.0.1 resources.zune.net mix.zune.net mix-ssl.zune.net
127.0.0.1 socialapi.zune.net socialapi-ssl.zune.net
127.0.0.1 inbox.zune.net inbox-ssl.zune.net
127.0.0.1 tiles.zune.net tuners.zune.net login.zune.net login-ssl.zune.net
127.0.0.1 fai.music.metaservices.microsoft.com metaservices.zune.net
```

Use the gateway's LAN address instead of `127.0.0.1` if the client is on a
different machine.

## 3. TLS

The client uses `https://` for the `*-ssl` variants, `login`, and a few others.
Two options:

- **HTTP where accepted.** Several hosts are plain `http://` (catalog, image,
  mix, resources, socialapi, tiles, tuners). Point those at the gateway directly.
- **A local CA you trust.** Terminate TLS for the `https` hosts with the sample
  reverse proxy in [`deploy/legacy/`](../deploy/legacy/) and install its
  `tls internal` root CA on the client machine. This is the recommended path for
  a full end-to-end test.

## 4. Enable content corpora

Each corpus is external and untracked; unset ⇒ the service fails closed (`404`).

| Env var | Serves | Layout |
|---|---|---|
| `RESOURCES_CORPUS_ROOT` | `resources.zune.net` firmware | `KeelBaseline.cab`, `DracoBaseline.cab`, … |
| `RESOURCES_TILES_ROOT` | `tiles.zune.net` | `Background/*.jpg`, `Avatar/*.jpg` |
| `RESOURCES_APPS_ROOT` | read-only app catalog | a tree of `*.zcp` |
| `RESOURCES_TUNERS_ROOT` | `tuners.zune.net` | PC-client resources |

## 5. Login (optional, gated)

The WS-Trust bridge at `login.zune.net` validates against the Dorado account
store but is **disabled by default** pending a security review:

```
LEGACY_LOGIN_ENABLED=true
```

On success the bridge returns a `wsse:BinarySecurityToken Id="Compact1"` ticket.
The client re-sends it as `Authorization: WLID1.0 <ticket>`; Dorado resolves it
to the account via the hash-only `LegacySession` store (`Legacy:Session:TtlHours`,
default 720 h).

## 6. Smoke test

```bash
tools/legacy-smoke.sh http://127.0.0.1:5080
```

It issues host-header requests to every legacy host and reports status codes.

## Known limitations

- `image.catalog.zune.net/music/artist/{id}/{type}` returns `404` until a
  keyless artist-image provider is added (`image` covers front covers).
- Movie/video catalog hubs return valid empty feeds.
- `commerce.zune.net` purchase/billing and Zune-Pass DRM/license endpoints are
  intentionally not implemented.
