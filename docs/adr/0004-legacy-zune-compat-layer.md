# ADR 0004 — Legacy Zune compatibility layer

- **Status:** Accepted
- **Date:** 2026-09-11

## Context

The modern Dorado Cloud API (`/v1/{module}`, JSON) serves the re-created
Dorado desktop and Android clients. A Zune 4.8 desktop installation or a Zune HD
device, however, was written against the original Microsoft web services and
speaks a different protocol to different host names:

| Original host | Purpose |
|---|---|
| `catalog.zune.net` | music/podcast catalog, Atom/XML (`v3.2`, `v4.0`) |
| `image.catalog.zune.net` | catalog artwork |
| `resources.zune.net` | firmware update manifest + baseline CABs |
| `tiles.zune.net` | profile tiles (backgrounds/avatars) |
| `mix.zune.net` | Mixview "similar tracks" |
| `login.zune.net` | WS-Trust `RST2.srf` session login |
| `socialapi.zune.net` | members / friends / badges |
| `inbox.zune.net` | messaging |
| `tuners.zune.net` | PC client resources served to the device |

Community projects have documented and partially recreated these hosts
(`ZuneDev/ZuneNetApi`, `ZuneDev/PyZuneResourcesServer`, `ZuneDev/PyZuneCatalogServer`,
`ZuneDev/Wiki`) and a live community mirror exists at `resources.zune.net` via a
hosts-file redirect. Users route the dead host names to a reachable server with
a hosts file (or DNS).

Reusing the modern modular monolith is preferable to standing up a second,
parallel stack: identity, catalog, artwork, social and QuickMix already exist
here and can back the legacy shapes.

## Decision

Add a **legacy Zune compatibility layer** to Dorado Cloud:

1. **Host dispatch, not a second host prefix.** Legacy modules extend
   `LegacyModuleBase`, mount at the root, and declare the host names they answer
   for via `RequireHost`. The request `Host` header selects the service; the
   modern `/v1/{module}` surface is unaffected.
2. **Reuse existing services.** `catalog.zune.net` and `image.catalog.zune.net`
   are backed by the shipped MusicBrainz + Cover Art Archive adapters and the
   artwork CDN; `mix.zune.net` by QuickMix; `socialapi.zune.net` by the social
   module. No new metadata provider keys are required.
3. **Atom/XML wire shapes.** A small `DoradoCloud.Legacy` library emits the
   Atom feeds and service-local XML the client parses. Response shapes are
   authored by Dorado from the documented protocol; no Microsoft feed or binary
   is copied.
4. **External corpora only.** Firmware baseline CABs, device app packages and
   PC-client resources are streamed from a configured, untracked corpus root
   (for example the Zune Archive mirror). Nothing from Microsoft is committed.
5. **Legal floor preserved (ADR 0003).** `commerce.zune.net` purchase/billing and
   any DRM/license acquisition endpoint is **out of scope** and returns `501`.
   There is no DRM circumvention and no license-server emulation.

## Consequences

- One deployment serves both the modern clients and hosts-patched Zune clients.
- Legacy modules are self-contained slices; they can be extracted later without
  changing the modern routes.
- The gateway preserves the original `Host` (`RequestHeaderOriginalHost: true`)
  so host dispatch works behind the reverse proxy.
- Corpora remain external and untracked; a missing corpus degrades to `404`
  rather than failing startup or shipping content.
- The `login.zune.net` WS-Trust bridge is security-sensitive and is implemented
  last, behind a documented review gate.
