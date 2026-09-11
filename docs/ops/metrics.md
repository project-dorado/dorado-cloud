# Metrics & dashboards

Observability is opt-in: set `Otel:Endpoint` (OTLP/HTTP) and the API exports
traces and metrics. Anything speaking OTLP works (OpenTelemetry Collector →
Prometheus/Tempo/Loki, or a hosted backend).

```bash
OTEL_ENDPOINT=http://otel-collector:4317 docker compose up -d
```

## Health probes

| Endpoint | Purpose |
|---|---|
| `GET /health/live` | liveness (process is up) |
| `GET /health/ready` | readiness (dependencies resolved) |
| `GET /health` (gateway) | gateway liveness |

## Signals worth graphing

ASP.NET Core instrumentation (enabled by `AddAspNetCoreInstrumentation`):

- `http.server.request.duration` — request latency (p50/p95/p99) and rate, by
  route and status. Watch `429` (auth rate limit) and `5xx`.
- `http.server.active_requests` — concurrency.
- `http.client.request.duration` — outbound latency to MusicBrainz, Cover Art
  Archive, Podcast Index, Radio-Browser, Wikidata, Fanart.tv, TheAudioDB,
  Discogs, Internet Archive. Provider latency/errors show up here.
- .NET runtime counters (GC, thread pool) via the standard meters.

Useful derived panels:

- **Error ratio** — 5xx / total, by route.
- **Auth pressure** — `/connect/token` + `/account/*` rate and 4xx.
- **Provider health** — client error rate per host (the `HostRateLimiter` also
  caps outbound rate, so a backlog shows as rising duration).
- **Legacy hosts** — request rate by `Host`, to see who is using `*.zune.net`.

A minimal starting dashboard is provided at
[`deploy/observability/grafana-dashboard.json`](../../deploy/observability/grafana-dashboard.json);
import it into Grafana and point it at your Prometheus data source. Metric names
are the Prometheus-normalized OTLP names and may need adjusting for your
collector configuration.

## Logs

Structured logs (JSON in production) include a request id; correlate with traces
via the trace/span id emitted by the ASP.NET instrumentation.
