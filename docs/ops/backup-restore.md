# Backup & restore

Dorado Cloud stores state in three places. Corpi (firmware/apps/PC-client
resources) are external and untracked, so they are backed up separately if you
care about them.

| State | Where | Backup |
|---|---|---|
| Accounts, devices, settings, social, updates, inbox, sessions, media metadata, embeddings | PostgreSQL | `pg_dump` |
| Artwork / media bytes | MinIO (S3) bucket | `mc mirror` |
| Signing & encryption keys, `.env` | `data/keys`, `.env` | file copy / secret manager |

## PostgreSQL

```bash
# backup
docker compose exec -T postgres pg_dump -U dorado -Fc doradocloud > doradocloud-$(date +%F).dump

# restore (into a fresh database)
docker compose exec -T postgres pg_restore -U dorado -d doradocloud --clean --if-exists < doradocloud-YYYY-MM-DD.dump
```

The API applies EF Core migrations at startup, so restoring a dump from an older
version and then starting the current image upgrades the schema.

## Object storage (MinIO)

```bash
mc alias set dorado http://localhost:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD"
mc mirror --overwrite dorado/"${MINIO_BUCKET:-dorado-cloud}" ./minio-backup
# restore
mc mirror --overwrite ./minio-backup dorado/"${MINIO_BUCKET:-dorado-cloud}"
```

Artwork is content-addressed and reconstructible from providers, so losing it is
recoverable (slower first paint); media bytes are not reconstructible if the
source disappears — keep those backed up.

## Keys & configuration

- `data/keys/openiddict-signing.pfx`, `openiddict-encryption.pfx`,
  `updates-signing.pem` — losing the signing key invalidates existing access
  tokens; losing the encryption key makes stored tokens unreadable.
- `.env` (Postgres/MinIO secrets, `Auth:Issuer`, provider keys).

Back these up to a secret manager, not the same host.

## Restore checklist

1. Restore `.env` and `data/keys` to the host.
2. Start Postgres/MinIO, restore the dump and the bucket.
3. `docker compose up -d` and confirm `GET /health/ready` returns `200`.
4. Verify: `GET /.well-known/openid-configuration`, a client-credentials token,
   and `GET /v1/identity/me` with a bearer token.
5. Smoke the legacy hosts: `tools/legacy-smoke.sh http://localhost:5088`.
