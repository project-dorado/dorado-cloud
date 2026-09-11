# pgvector acceleration (optional)

By default the similarity embedding store (M10) keeps vectors as JSON text and
ranks them with an in-memory cosine scan. That works everywhere (SQLite and
PostgreSQL) and keeps the deployment dependency-free.

For larger corpora you can accelerate the nearest-neighbour query with
[pgvector](https://github.com/pgvector/pgvector):

1. Run PostgreSQL with the extension, e.g. swap the image in
   `docker-compose.yml`:

   ```yaml
   postgres:
     image: pgvector/pgvector:pg16
   ```

2. After the API has applied its EF migrations (so `TrackEmbeddings` exists),
   add the vector column and HNSW index:

   ```bash
   psql "$ConnectionStrings__Postgres" -f deploy/pgvector/init.sql
   ```

3. Enable the accelerated path:

   ```
   Embedding__UsePgvector=true
   ```

`EmbeddingService` writes both the JSON column (source of truth) and the
`vector` column, and prefers the `<=>` operator for reads. If the extension or
column is missing it logs and transparently falls back to the in-memory scan, so
this remains safe to toggle.
