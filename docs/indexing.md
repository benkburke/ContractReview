# Indexing pipeline

Each contract's extracted text is split into section-tagged chunks and each chunk is embedded, so T3 can retrieve text by vector similarity (D-022). Spec: `specs/contract-chunks-embeddings.md`.

## Flow

1. **Chunker** (`ContractChunker`, Application). Pure function. Detects section headings (upper-case lines), splits each section on blank lines and packs paragraphs into chunks of up to `Indexing:ChunkSize` characters with `Indexing:ChunkOverlap` overlap. Chunks never cross sections. Text before the first heading goes in the `PREAMBLE` section.
2. **Embedding client** (`IEmbeddingClient`, Application; `OpenRouterEmbeddingClient` or `FakeEmbeddingClient`, Infrastructure). Each chunk is embedded as `<heading>` + newline + chunk text, in batches of `Indexing:EmbeddingBatchSize`. The client returns one vector per input and throws `EmbeddingFailedException` on any failure.
3. **Storage** (`IndexContractHandler`). The contract moves to `Indexing`, the chunks and vectors are built, then old and new chunks are swapped in one save: the `ContractChunks` rows are replaced and the status becomes `Indexed`. On failure the status becomes `Failed` with the error code and the contract is kept.

Callers: `UploadContract` (after saving the contract, so a failure still returns 201 with `Failed`), `POST /api/contracts/{id}/index`, and `ContractIndexBackfillService` (startup).

## Storage

- `Contracts`: `IndexStatus`, `IndexError`, `IndexedAtUtc`, `EmbeddingModel`, `EmbeddingDimensions`.
- `ContractChunks`: one row per chunk with `SectionOrder`, `SectionHeading`, `ChunkOrder`, `Text` and `Embedding`. Deleting a contract deletes its chunks.
- Vectors are `varbinary(max)` holding little-endian float32 values. The model name and dimension are stored with the contract.
- LocalDB runs SQL Server 2019, which has no native vector type. Similarity is computed in-process (cosine), not in SQL. The API never returns vectors.

## Status

`NotIndexed` (default for existing contracts after migration) → `Indexing` → `Indexed` or `Failed`. Only `Indexed` contracts have `sections`.
