# NOTICE — third-party materials in Dorado Cloud

## Runtime dependencies
- **.NET 8 / ASP.NET Core** — MIT License.
- **OpenIddict** — Apache-2.0. OAuth 2.0 / OpenID Connect server and validation.
- **Entity Framework Core** (+ Npgsql, Sqlite providers) — MIT License.
- **YARP** — MIT License. Reverse proxy.
- **StackExchange.Redis** — MIT License.
- **OpenTelemetry .NET** — Apache-2.0.

## Data sources

Dorado Cloud consumes openly licensed metadata/artwork with attribution and
per-provider rate limits. Nothing below is bundled. MusicBrainz/Cover Art
Archive, Podcast Index and Radio-Browser are consumed by the shipped M2–M5
adapters; the remaining rows are planned (native-provider enrichment or M6).

| Source | License / terms | Used for |
|---|---|---|
| MusicBrainz + Cover Art Archive | CC0 / public domain metadata | catalog, MBIDs, cover art (shipped, M3) |
| Podcast Index | API terms | podcast directory (shipped, M2) |
| Radio-Browser | public-domain catalog | radio directory (shipped, M2) |
| Fanart.tv | API terms (key required) | artist artwork (optional adapter; disabled without a key) |
| TheAudioDB | API terms | artist artwork (optional adapter; disabled without a key) |
| Discogs | API terms (token required) | artist imagery/enrichment (optional adapter; disabled without a token) |
| Wikipedia / Wikimedia | CC BY-SA | artist biographies, images (allowlisted proxy) |
| AcoustID / Chromaprint | public API + tool | scan-time fingerprinting in the desktop client |
| ListenBrainz / AcousticBrainz | open data | recommendations (pgvector upgrade — planned) |
| Internet Archive / Wikimedia Commons | PD / CC only | DRM-free streaming (M6, legal-gated) |

## Legacy Zune compatibility
- The legacy `*.zune.net` compatibility hosts serve **metadata** from the same
  open providers above and **stream** firmware CABs, `.zcp` app packages and
  PC-client resources only from an operator-supplied, untracked corpus. No
  Microsoft firmware, packages, fonts or artwork are bundled or committed.
- `commerce.zune.net` purchase/billing and Zune-Pass DRM or license acquisition
  are not implemented; no DRM is circumvented and no license server is emulated.
- The `login.zune.net` WS-Trust bridge is disabled by default and validates
  against the local account store only.

## Trademarks
- Zune, Zegoe, Zune HD and Microsoft are trademarks of Microsoft Corporation.
  Dorado is an independent, non-affiliated homage.

## Policy
- No Microsoft code, binaries, fonts, firmware or artwork are redistributed.
- No DRM circumvention and no license-server emulation.
- No copyrighted media is hosted; only public-domain / Creative Commons content
  may be streamed, and only after legal review.
