# Moderation operations

Dorado Cloud's community surface (profiles, follow graph, activity feed, Zune
Card, inbox) needs operator tooling for abuse. This is the runbook.

## Grant operator rights

Admin/operator rights are fail-closed: with an empty allowlist, only the
**Development** environment has them. Configure at least one of:

```
Admin__Subjects__0=operator@example.com     # or an OIDC subject id
# Updates__Admins__0=operator@example.com    # publish-only role
```

`Admin` covers moderation **and** publishing; `UpdatesAdmin` covers publishing
only. Users authenticate through the normal account flow.

## Reports queue

```bash
# list open reports
curl -H "Authorization: Bearer $TOKEN" \
     "https://cloud.example/v1/social/admin/reports?status=open"

# resolve
curl -X POST -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
     -d '{"status":"actioned"}' \
     "https://cloud.example/v1/social/admin/reports/{id}/resolve"
```

Statuses are free-form strings (`open`, `reviewing`, `actioned`, `dismissed`);
pick a convention and keep it.

## Blocking and escalation

- Users self-serve `POST/DELETE /v1/social/profiles/{handle}/block`; blocks
  exclude activity **in both directions** from feeds.
- File reports with `POST /v1/social/reports`.
- For account-level abuse, use the Identity admin surface:
  - `GET /v1/identity/me` (self), GDPR erasure via `DELETE /v1/identity/me`
    (operator-invoked on behalf of a subject through the admin tooling of your
    choice; it revokes OIDC tokens and deletes associated data).
- Legacy inbox messages are tied to the session account; abuse in
  `inbox.zune.net` is visible via the `InboxMessage` store.

## Content policy

- No copyrighted media is hosted. The `media` module is legal-gated
  (`Media:Enabled`) and only accepts PD/CC licenses on the allowlist.
- Firmware/apps/PC-client resources are streamed from operator corpora; you are
  responsible for what you mount.
- `commerce.zune.net` purchase/billing and Zune-Pass DRM/license endpoints are
  intentionally not implemented — do not add them.
