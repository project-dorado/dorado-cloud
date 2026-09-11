# Legacy Zune TLS reverse proxy

A small [Caddy](https://caddyserver.com/) configuration that terminates TLS for
the `https://*.zune.net` hosts with a locally-trusted CA and forwards to the
Dorado Cloud gateway, preserving the original `Host` header (required for
host dispatch).

## Run

```bash
# Point at your gateway (defaults to http://127.0.0.1:5088)
export DORADO_UPSTREAM=http://127.0.0.1:5088

caddy run --config deploy/legacy/Caddyfile
# first run: install Caddy's local root CA on the client machine
caddy trust
```

Then add the hosts-file entries from
[`docs/legacy-client-setup.md`](../../docs/legacy-client-setup.md).

## Notes

- `local_certs` issues certificates from Caddy's internal CA — appropriate for a
  LAN/test setup, not the public internet.
- The plain-HTTP hosts are routed by the second block and need no TLS.
- If you already terminate TLS elsewhere (nginx, Traefik), the only requirement
  is that the original `Host` header reaches the API (or that
  `X-Forwarded-Host` is honoured).
