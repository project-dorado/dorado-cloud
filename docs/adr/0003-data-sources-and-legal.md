# ADR 0003 — Data sources and legal policy

- **Status:** Accepted
- **Date:** 2026-09-10

## Context

Zune's original services (Marketplace, Zune Pass, Social, QuickMix, podcast/
radio catalogs, artist imagery) are dead, and their protocols and DRM keys are
not recoverable. Reviving them faithfully is neither feasible nor lawful. At the
same time, users want the *function*, and Dorado clients already work offline.

## Decision

Recreate the **function** using openly licensed data, with a strict legal floor:

1. **No DRM circumvention / no license-server emulation.**
2. **No copyrighted media hosted.** Metadata + link-out, or the user's own files.
   Streaming is limited to **public-domain / Creative Commons** content (M6),
   after legal review.
3. **Provider terms honored** (attribution, rate limits, API keys): MusicBrainz +
   Cover Art Archive, Fanart.tv, TheAudioDB, Wikipedia/Wikimedia, ListenBrainz/
   AcousticBrainz, Podcast Index, Radio-Browser, Internet Archive.
4. **Trademarks respected:** services are branded "Dorado Cloud", never
   "Zune Marketplace/Social"; the non-affiliation notice is shown to users.
5. **No Microsoft code, binaries, fonts, firmware or artwork** are redistributed.

## Consequences

- Legal, sustainable, and community-friendly.
- Features differ from the original where the original depended on DRM/marketplace
  (e.g. no purchased-content sync) — documented as deliberate substitutions.
- Each provider adapter must implement attribution + caching/rate limiting, and
  new sources require a license check before adoption.
