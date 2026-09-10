# NOTICE — third-party materials in Dorado Cloud

## Runtime dependencies
- **.NET 8 / ASP.NET Core** — MIT License.
- **OpenIddict** — Apache-2.0. OAuth 2.0 / OpenID Connect server and validation.
- **Entity Framework Core** (+ Npgsql, Sqlite providers) — MIT License.
- **YARP** — MIT License. Reverse proxy.
- **StackExchange.Redis** — MIT License.
- **OpenTelemetry .NET** — Apache-2.0.

## Data sources (used by later milestones)
Dorado Cloud will consume openly licensed metadata/artwork with attribution and
per-provider rate limits. Nothing below is bundled; adapters land in M2–M5.

| Source | License / terms | Used for |
|---|---|---|
| MusicBrainz + Cover Art Archive | CC0 / public domain metadata | catalog, MBIDs, cover art |
| Fanart.tv | API terms (key required) | artist artwork |
| TheAudioDB | API terms | artist/album artwork |
| Wikipedia / Wikimedia | CC BY-SA | artist biographies, images |
| ListenBrainz / AcousticBrainz | open data | recommendations (M5) |
| Podcast Index | API terms | podcast directory (M2) |
| Radio-Browser | public-domain catalog | radio directory (M2) |
| Internet Archive / Wikimedia Commons | PD / CC only | DRM-free streaming (M6) |

## Trademarks
- Zune, Zegoe, Zune HD and Microsoft are trademarks of Microsoft Corporation.
  Dorado is an independent, non-affiliated homage.

## Policy
- No Microsoft code, binaries, fonts, firmware or artwork are redistributed.
- No DRM circumvention and no license-server emulation.
- No copyrighted media is hosted; only public-domain / Creative Commons content
  may be streamed, and only after legal review.
