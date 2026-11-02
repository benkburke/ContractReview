# Section-aware chunks and embeddings
Status: done 2026-11-02
Roadmap: T2 (thin)

## Context
Second thin-path step. T3 retrieves contract text by vector similarity (D-022), so every contract needs chunks tagged with the section they come from (for example SCHEDULE OF COVERAGE, WHAT IS NOT COVERED) and an embedding per chunk, computed via OpenRouter (D-016) and stored in SQL Server. This item adds that indexing to upload, backfills contracts uploaded in T1, and shows the sections on the contract page. No retrieval or claims yet (T3), no live progress (T4).

## Sides involved
Both: backend (chunker, embedding client, indexing, schema, endpoint changes) and frontend (index status and sections on the contract page, retry button).

## Acceptance criteria
1. Given a valid PDF, when uploaded, then the contract is stored, split into section-tagged chunks, each chunk is embedded, chunks and vectors are stored, and the response and detail page show `indexStatus = Indexed`.
2. Given an indexed contract, when its page is opened, then it lists the sections in document order with heading and chunk count, above the extracted text.
3. Given the embedding service fails or times out during upload, when uploaded, then the contract and its text are still stored (no data loss), `indexStatus = Failed`, the response is still 201, and the page shows "Indexing failed" with a **Retry indexing** button. (D-026)
4. Given a contract with `indexStatus` NotIndexed, Failed or Indexing (stale), when the API starts, then a background backfill indexes it; T1 contracts get NotIndexed from the migration.
5. Given a Failed or NotIndexed contract, when the user presses Retry indexing (`POST /api/contracts/{id}/index`), then it is re-chunked and re-embedded; on success the page shows Indexed and the sections; on failure it stays Failed with the error message shown.
6. Given an unknown id, when `POST /api/contracts/{id}/index` is called, then 404 `contract.not_found`. Given a contract currently Indexing, then 409 `contract.indexing_in_progress`.
7. Re-indexing replaces the previous chunks atomically (never a mix of old and new chunks).

## Endpoints
| Method | Route | Request | Success | Errors |
|---|---|---|---|---|
| POST | `/api/contracts` | unchanged | 201 `ContractSummaryDto` (+ `indexStatus`) | unchanged |
| GET | `/api/contracts` | – | 200 `ContractSummaryDto[]` (+ `indexStatus`) | – |
| GET | `/api/contracts/{id:guid}` | – | 200 `ContractDetailDto` (+ `indexStatus`, `sections`) | 404 |
| POST | `/api/contracts/{id:guid}/index` | no body | 200 `ContractDetailDto` | 404, 409, 502 `contract.embedding_failed` |

- `indexStatus`: string enum `NotIndexed | Indexing | Indexed | Failed`.
- `ContractSectionDto`: `order` (int, 0-based), `heading` (string), `chunkCount` (int).
- `ContractDetailDto.sections`: `ContractSectionDto[]` in document order; empty unless Indexed.
- Vectors are never returned by the API.

## Chunking rules
- **Section heading:** a trimmed line of 3–100 characters with at least 3 letters, where every letter is upper case (digits, spaces and `& , . : ( ) - /` allowed), for example `SCHEDULE OF COVERAGE`, `WHAT IS NOT COVERED`. Consecutive heading lines merge into one heading (joined by a space).
- Text before the first heading goes in a section named by the constant `PREAMBLE`.
- **Chunks:** within a section, split on blank lines into paragraphs and pack them into chunks of at most **1,200 characters**; a paragraph longer than that is split at sentence ends, else hard-split. **150-character overlap** between consecutive chunks of the same section. Chunks never cross sections. Each chunk stores its section heading, section order and chunk order. Embedded text is `"<heading>\n<chunk text>"` so the section informs similarity. (Sizes are options, `Indexing:ChunkSize` / `Indexing:ChunkOverlap`.)
- Empty or whitespace-only chunks are dropped. A contract with no chunks after splitting is Failed (cannot happen for valid T1 uploads, which require text).
- **Limit:** at most **2,000 chunks** per contract (`Indexing:MaxChunks`); over that, indexing fails with `contract.too_many_chunks` (the contract stays stored, status Failed).

## Embeddings
- `IEmbeddingClient` (Application) → `OpenRouterEmbeddingClient` (Infrastructure): OpenAI-compatible `POST {BaseUrl}/embeddings`, batches of up to 64 inputs, 30 s timeout per call, standard retry on 429/5xx (2 retries). (D-027)
- Options `OpenRouter:BaseUrl` (default `https://openrouter.ai/api/v1`), `OpenRouter:EmbeddingModel` (default `openai/text-embedding-3-small`, 1536 dims). `OpenRouter:ApiKey` comes from user-secrets / env vars only (never in appsettings or git).
- Missing API key: the app still starts; indexing fails with `contract.embedding_failed` and the contract is Failed.
- Vectors stored as `varbinary(max)` (float32, little-endian); dimension and model name stored with the contract. LocalDB is SQL Server 2019 with no native vector type; T3 computes cosine similarity in-process.
- **Tests and e2e** (D-028): `Embeddings:Provider = Fake` selects a deterministic local embedder (hashed bag-of-words, normalized, fixed dimension) so tests and CI never call OpenRouter. Default is `OpenRouter`. Playwright's backend `env` sets `Embeddings__Provider=Fake`.

## Validation rules and limits
- Upload inputs: unchanged from T1.
- `id` route value: GUID; unknown → 404.
- Indexing options validated at startup: ChunkSize 200–8,000; overlap 0 to ChunkSize/2; MaxChunks 1–10,000; batch size 1–256.
- Embedding response: count must equal input count and every vector must have the same non-zero dimension; otherwise `contract.embedding_failed`.

## Realtime
None (indexing progress over SignalR is not planned; T4 covers claim evaluation only).

## Auth
None (D-003). Endpoints are anonymous.

## Domain model
- `Contract` aggregate gains `IndexStatus`, `IndexError` (code, nullable), `IndexedAtUtc`, `EmbeddingModel`, `EmbeddingDimensions`, and a child collection `ContractChunk` (entity: `SectionOrder`, `SectionHeading`, `ChunkOrder`, `Text`, `Embedding` bytes).
- Invariants: `ReplaceChunks(chunks, model, dims)` only from Indexing; every chunk's vector has `dims` floats; status moves NotIndexed/Failed → Indexing → Indexed | Failed; Indexed requires ≥ 1 chunk; starting indexing while Indexing throws `contract.indexing_in_progress` (except the startup backfill resetting stale Indexing).
- Application: `IContractChunker` (pure, in Application or Domain service), command `IndexContract(id)` used by upload, the retry endpoint and the backfill; `UploadContract` calls it after saving the contract; `GetContract` returns sections grouped from chunks.
- Infrastructure: `OpenRouterEmbeddingClient`, `FakeEmbeddingClient`, hosted service `ContractIndexBackfillService` (runs once at startup, one contract at a time, logs failures).

## Data model changes
Migration `AddContractChunks`:
- `Contracts`: add `IndexStatus` nvarchar(20) not null default `NotIndexed`, `IndexError` nvarchar(100) null, `IndexedAtUtc` datetime2 null, `EmbeddingModel` nvarchar(200) null, `EmbeddingDimensions` int null; index on `IndexStatus`.
- New `ContractChunks`: `Id` uniqueidentifier PK, `ContractId` FK → Contracts (cascade delete), `SectionOrder` int, `SectionHeading` nvarchar(200), `ChunkOrder` int, `Text` nvarchar(max), `Embedding` varbinary(max); unique index (`ContractId`, `SectionOrder`, `ChunkOrder`).

## Frontend
- Contract page: an index status chip (Indexed / Not indexed / Indexing / Indexing failed); when Indexed, a "Sections" list (heading + "n chunks") in document order; when Failed or NotIndexed, a message and a **Retry indexing** button (busy state while the request runs) that refreshes the page data on success and shows the ProblemDetails error on failure.
- Contracts list: small status chip per row.
- All text in `src/strings/`, error codes in `src/constants/errorCodes.ts`.

## Accessibility
Status chip has text (not colour only); the sections list is a semantic list with a heading; Retry button exposes busy state and is keyboard operable; the result of a retry is announced (`role="status"` for success, `role="alert"` for errors); axe clean.

## Out of scope (Depth)
Retrieval and claim evaluation (T3); indexing progress over SignalR; native SQL vector type or a vector database; manual section editing; viewing chunk text in the UI; re-indexing all contracts when the embedding model changes (add to Depth as D9); full failure-scenario tests (D7).

## Open questions
None. Failure keeps the contract as Failed with Retry (D-026); model openai/text-embedding-3-small (D-027); fake embedder in tests, CI and e2e (D-028).
