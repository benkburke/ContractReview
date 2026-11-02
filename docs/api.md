# API reference

Source of truth: `backend/openapi/openapi.json` (generated at build time). Base URL in development: `https://localhost:7080` or `http://localhost:5080`. There is no authentication (D-003).

Errors use `application/problem+json` (ProblemDetails). Domain failures carry a stable `code` field (see [Error codes](#error-codes)).

## Contracts

### POST /api/contracts

Uploads a text-based PDF contract. Operation id `UploadContract`.

Request: `multipart/form-data`

| Field | Type | Rules |
|-------|------|-------|
| `title` | string | Required, trimmed, 1 to 200 characters. |
| `file` | binary | Required. Name ends in `.pdf` (case-insensitive), 1 to 255 characters, content type `application/pdf`, first bytes `%PDF-`, size 1 byte to the limit in `Upload:MaxFileBytes` (default 10 MB, D-012). Scanned or image-only PDFs are rejected (D-013). |

Responses:

- `201 Created`: body is `ContractSummaryDto`. `Location` header holds the URL of the new resource.
- `400 Bad Request`: ProblemDetails with `code` set to one of `contract.file_not_pdf`, `contract.file_too_large`, `contract.title_invalid`, `contract.no_text`, `contract.pdf_unreadable`. Nothing is stored.
- `413 Payload Too Large`: ProblemDetails with `code` `contract.file_too_large`. The request exceeded the server body limit (`Upload:MaxFileBytes` plus 1 MiB). Nothing is stored.

### GET /api/contracts

Lists stored contracts, newest first. Operation id `ListContracts`.

- `200 OK`: array of `ContractSummaryDto` (each with `indexStatus`).

### GET /api/contracts/{id}

Returns one contract with its extracted text, index status and sections. Operation id `GetContract`.

| Parameter | In | Type | Required |
|-----------|----|------|----------|
| `id` | path | uuid | yes |

- `200 OK`: `ContractDetailDto`.
- `404 Not Found`: ProblemDetails with `code` `contract.not_found`.

### POST /api/contracts/{id}/index

Re-chunks and re-embeds an existing contract (the Retry indexing action). Operation id `IndexContract`. No request body.

The same indexing runs automatically after upload and at startup backfill (see [Indexing](indexing.md)). Re-indexing replaces the previous chunks in one transaction.

| Parameter | In | Type | Required |
|-----------|----|------|----------|
| `id` | path | uuid | yes |

- `200 OK`: `ContractDetailDto` with `indexStatus` `Indexed` and the new `sections`.
- `404 Not Found`: ProblemDetails with `code` `contract.not_found`.
- `409 Conflict`: ProblemDetails with `code` one of:
  - `contract.indexing_in_progress`: the contract is already `Indexing`.
  - `contract.too_many_chunks`: the text splits into more than `Indexing:MaxChunks` chunks. The contract is set to `Failed`.
  - `contract.no_text`: no chunks remain after splitting. The contract is set to `Failed`.
- `502 Bad Gateway`: ProblemDetails with `code` `contract.embedding_failed` when the embedding service fails, times out, or returns an unusable response (wrong count or dimension). The contract is set to `Failed`.

A failed run keeps the contract and its extracted text; only the status changes. Call the endpoint again to retry.

## Schemas

`ContractSummaryDto`

| Property | Type | Notes |
|----------|------|-------|
| `id` | uuid | |
| `title` | string | |
| `fileName` | string | |
| `sizeBytes` | int64 | Serialized as a JSON number or string. |
| `pageCount` | int32 | Serialized as a JSON number or string. |
| `uploadedAtUtc` | date-time | UTC. |
| `indexStatus` | `IndexStatus` | `NotIndexed`, `Indexing`, `Indexed` or `Failed`. |

`ContractDetailDto` has every `ContractSummaryDto` property plus:

| Property | Type | Notes |
|----------|------|-------|
| `extractedText` | string | Text extracted from the PDF, line breaks preserved. |
| `sections` | `ContractSectionDto[]` | Sections in document order. Empty unless `indexStatus` is `Indexed`. Vectors are never returned. |

`IndexStatus` is a string enum: `NotIndexed` (never indexed, or T1 contracts before backfill), `Indexing`, `Indexed`, `Failed` (see `POST /api/contracts/{id}/index`).

`ContractSectionDto`

| Property | Type | Notes |
|----------|------|-------|
| `order` | int32 | 0-based position in the document. |
| `heading` | string | Section heading, for example `SCHEDULE OF COVERAGE`. Text before the first heading uses `PREAMBLE`. |
| `chunkCount` | int32 | Number of chunks in the section. |

`ProblemDetails` has the standard fields (`type`, `title`, `status`, `detail`, `instance`) and `code` (string, nullable).

## Error codes

| Code | HTTP status | Meaning |
|------|-------------|---------|
| `contract.file_not_pdf` | 400 | Wrong extension, content type, or missing `%PDF-` header. |
| `contract.file_too_large` | 400, or 413 when the request exceeds the server body limit | File is over `Upload:MaxFileBytes`. The 413 body also carries this code. |
| `contract.title_invalid` | 400 | Title is missing, blank, or over 200 characters. |
| `contract.no_text` | 400 | PDF has no extractable text (scanned or image-only). |
| `contract.pdf_unreadable` | 400 | PDF is corrupt or encrypted. |
| `contract.not_found` | 404 | No contract with the given id. |
| `contract.indexing_in_progress` | 409 | Indexing is already running for the contract. |
| `contract.too_many_chunks` | 409 | Text splits into more than `Indexing:MaxChunks` chunks. Contract is `Failed`. |
| `contract.embedding_failed` | 502 | Embedding service failed, timed out, or returned an unusable response. Contract is `Failed`. The reason is only in the logs. |

`contract.no_text` also appears as 409 on `POST /api/contracts/{id}/index` when no chunks remain after splitting.

The code list lives in `backend/src/ContractReview.Domain/Errors/ErrorCodes.cs`.
