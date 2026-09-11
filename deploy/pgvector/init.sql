-- Dorado Cloud — optional pgvector acceleration for similarity embeddings (M10).
--
-- Only needed when Embedding:UsePgvector=true. Requires a PostgreSQL server with
-- the `vector` extension (e.g. the `pgvector/pgvector:pg16` image) and the
-- TrackEmbeddings table created by the EF migration.
--
--   psql "$ConnectionStrings__Postgres" -f deploy/pgvector/init.sql
--
-- After the EF migration has created "TrackEmbeddings", this adds the vector
-- column and an HNSW cosine index. EmbeddingService writes both the portable
-- JSON column and this vector column, and prefers `<=>` for reads.

CREATE EXTENSION IF NOT EXISTS vector;

ALTER TABLE "TrackEmbeddings"
    ADD COLUMN IF NOT EXISTS "Embedding" vector(1536);

CREATE INDEX IF NOT EXISTS "IX_TrackEmbeddings_Embedding"
    ON "TrackEmbeddings" USING hnsw ("Embedding" vector_cosine_ops);
