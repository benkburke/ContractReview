# Coverage: contract-chunks-embeddings (T2, thin mode)

Backend integration: `backend/tests/ContractReview.IntegrationTests/ContractIndexingTests.cs`. Chunker unit tests: `backend/tests/ContractReview.UnitTests/Application/ContractChunkerTests.cs`. Vitest: `frontend/src/__tests__/IndexingPanel.test.tsx`, `SectionList.test.tsx`. E2E: `frontend/e2e/indexing.spec.ts`.

## Acceptance criteria
| AC | Happy | Critical failure | Notes |
|---|---|---|---|
| 1 upload indexes | covered (integration, e2e) | covered | status, sections, chunks and vectors persisted |
| 2 sections in order | covered (integration, Vitest, e2e) | n/a | |
| 3 embedding fails at upload | n/a | covered (stub embedder: 201, Failed, text kept, no chunks, error code stored) | |
| 4 backfill | covered (NotIndexed, Failed, stale Indexing via `RunAsync`) | n/a | |
| 5 retry | covered (200 success; 502 then recovery; Vitest success/error/busy) | covered | |
| 6 404 / 409 | covered | covered | |
| 7 atomic replace | covered (new chunk ids, no duplicates; failed re-index keeps old chunks) | covered | |

## Endpoints
| Endpoint | Happy | Null/missing | Empty/whitespace | Bounds | Invalid input | 404/409 | Auth |
|---|---|---|---|---|---|---|---|
| POST /api/contracts (+indexStatus) | covered | unchanged (T1 tests) | unchanged | unchanged | unchanged | n/a | n/a (D-003) |
| GET /api/contracts (+indexStatus) | covered | n/a | n/a | deferred (paging n/a) | n/a | n/a | n/a |
| GET /api/contracts/{id} (+sections) | covered | n/a | n/a | n/a | T1 | T1 | n/a |
| POST /api/contracts/{id}/index | covered | n/a (no body) | n/a | n/a | non-GUID id: deferred | 404, 409 covered; 409 too_many_chunks and no-chunks: deferred | n/a |

## Deferred (thin mode)
- MaxChunks over-limit (`contract.too_many_chunks`) integration test; options validation bounds at startup (ChunkSize, overlap, MaxChunks, batch size).
- Embedding response validation (count mismatch, dimension mismatch, empty vectors) unit tests for the handler.
- OpenRouterEmbeddingClient HTTP behavior (batching, retries on 429/5xx, timeout, missing API key) with a stub HttpMessageHandler.
- Domain unit tests for `Contract` indexing invariants (ReplaceChunks guards, StartIndexing while Indexing).
- Concurrent retry race (DbUpdateConcurrencyException to 409).
- Non-GUID id on the index route; e2e for the Failed and Retry flow; list-row chip test.
