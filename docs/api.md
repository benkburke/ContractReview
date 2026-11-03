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

## Claims

A claim is a natural-language question asked against one indexed contract (D-017). Evaluation is synchronous: the request returns when the claim is stored. Expect it to take up to a minute. Endpoints are anonymous (D-003). Spec: `specs/evaluate-claim.md`.

### POST /api/contracts/{id}/claims

Evaluates a claim against the contract. Operation id `EvaluateClaim`.

| Parameter | In | Type | Required |
|-----------|----|------|----------|
| `id` | path | uuid | yes |

Optional header `X-SignalR-Connection-Id` receives live progress events on the claim-progress hub (see [Realtime](realtime.md)). Without it, no progress is sent.

Request body: `EvaluateClaimRequest` (`application/json`).

| Field | Type | Rules |
|-------|------|-------|
| `claimText` | string | Required, trimmed, 5 to 2,000 characters. |
| `coverageLevel` | string, nullable | Optional. When present, trimmed, 1 to 100 characters. Not checked against the contract's options; the LLM judges it. |

The API embeds the claim, retrieves the top `Claims:TopK` chunks, asks the chat model for a structured answer and stores the claim with its result (see [Claim evaluation](indexing.md#claim-evaluation)).

Responses:

- `201 Created`: body is `ClaimEvaluationDto` with `state` `Evaluated`. `Location` header holds the URL of the new claim (`GET /api/contracts/{id}/claims/{claimId}`).
- `400 Bad Request`: ProblemDetails with `code` `claim.text_invalid` or `claim.coverage_level_invalid`. Nothing is stored.
- `404 Not Found`: ProblemDetails with `code` `contract.not_found`. Nothing is stored.
- `409 Conflict`: ProblemDetails with `code` `contract.not_indexed` (the contract is not `Indexed`) or `contract.embedding_model_mismatch` (the contract was indexed with a different embedding model than the current one). Nothing is stored.
- `502 Bad Gateway`: ProblemDetails with `code` `claim.evaluation_failed` when the embedding or chat call fails, times out, returns output that does not match the schema (after one retry), or when the OpenRouter API key is missing. The claim is stored with `state` `Failed` and `errorCode` `claim.evaluation_failed`. No partial result is stored. The reason is only in the logs.

A `Covered`, `NotCovered` or `Conditional` answer without a valid citation is stored as `InsufficientInformation` (D-032). That is not an error.

### GET /api/contracts/{id}/claims/{claimId}

Returns a stored claim, including a `Failed` one. Operation id `GetClaim`. Use it to reload the results dashboard.

| Parameter | In | Type | Required |
|-----------|----|------|----------|
| `id` | path | uuid | yes |
| `claimId` | path | uuid | yes |

- `200 OK`: `ClaimEvaluationDto`.
- `404 Not Found`: ProblemDetails with `code` `contract.not_found` (no contract with `id`) or `claim.not_found` (no claim with `claimId` under that contract).

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

`EvaluateClaimRequest`: `claimText` (string, required), `coverageLevel` (string, nullable).

`ClaimEvaluationDto`

| Property | Type | Notes |
|----------|------|-------|
| `id` | uuid | |
| `contractId` | uuid | |
| `claimText` | string | |
| `coverageLevelRequested` | string, nullable | The level the user supplied, if any. |
| `state` | `ClaimState` | `Evaluated` or `Failed`. |
| `createdAtUtc` | date-time | UTC. |
| `result` | `ClaimEvaluationResultDto`, nullable | Null when `state` is `Failed`. |
| `errorCode` | string, nullable | Set when `state` is `Failed`. |
| `review` | `ClaimReviewDto`, nullable | Independent second-pass check of the answer. Null when not reviewed: `InsufficientInformation`, `needsCoverageLevel`, `Review:Enabled=false`, or claims created before the review feature. |

`ClaimEvaluationResultDto`

| Property | Type | Notes |
|----------|------|-------|
| `status` | `ClaimStatus` | `Covered`, `NotCovered`, `Conditional` or `InsufficientInformation`. |
| `confidence` | int32 | 0 to 100. Serialized as a JSON number or string. |
| `coverageLevel` | string, nullable | The level used or inferred from the contract. |
| `needsCoverageLevel` | boolean | True when the contract leaves the level blank and none was supplied. Then `coverageLevelOptions` lists the tier names. |
| `coverageLevelOptions` | string[] | Up to 10 tier names. |
| `coveredItems` | string[] | Up to 20 items, each up to 300 characters. |
| `excludedItems` | string[] | Up to 20 items, each up to 300 characters. |
| `reasoning` | string | Up to 4,000 characters. May note that state amendments can apply (D-021). |
| `followUpQuestion` | string, nullable | Set for vague claims; up to 500 characters. |
| `citations` | `ClaimCitationDto[]` | Only chunks that were actually retrieved. Up to 10. |
| `model` | string | The chat model that produced the answer, for example `openai/gpt-4o-mini`. |

`ClaimCitationDto`

| Property | Type | Notes |
|----------|------|-------|
| `sectionHeading` | string | Heading of the cited chunk's section. |
| `excerpt` | string | From the stored chunk text, at most 500 characters. |

`ClaimReviewDto`

| Property | Type | Notes |
|----------|------|-------|
| `state` | string | `Completed` or `Failed`. |
| `agreesWithPrimary` | boolean, nullable | Null when `state` is `Failed`. |
| `isCovered` | boolean, nullable | The reviewer's own verdict. |
| `confidence` | int32, nullable | 0 to 100. |
| `accuracyExplanation` | string, nullable | Up to 2,000 characters. |
| `coveredItems` | string[] | Up to 20 items, each up to 300 characters. |
| `notCoveredItems` | string[] | Up to 20 items, each up to 300 characters. |
| `model` | string, nullable | Review model, from `OpenRouter:ReviewModel`. |
| `errorCode` | string, nullable | `review.failed` when `state` is `Failed`. |

A failed review does not fail the claim. If the review call fails, times out or returns invalid output (after one retry), the claim is still returned with `201` and `state` `Evaluated`, and `review` is `{ state: "Failed", errorCode: "review.failed" }`. The primary result is unchanged.

`ClaimState` is a string enum: `Evaluated`, `Failed`. `ClaimStatus` is a string enum: `Covered`, `NotCovered`, `Conditional`, `InsufficientInformation`.

Chunk vectors and the raw prompt are never returned.

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
| `contract.not_indexed` | 409 | Claim submitted against a contract that is not `Indexed`. |
| `contract.embedding_model_mismatch` | 409 | Contract was indexed with a different embedding model than the current one. Re-index it. |
| `claim.text_invalid` | 400 | `claimText` is missing, or trimmed length is outside 5 to 2,000 characters. |
| `claim.coverage_level_invalid` | 400 | `coverageLevel` is present but trimmed length is outside 1 to 100 characters. |
| `claim.not_found` | 404 | No claim with the given id under that contract. |
| `claim.evaluation_failed` | 502 | Embedding or chat call failed, timed out, returned invalid output after one retry, or the API key is missing. The claim is stored as `Failed`. The reason is only in the logs. |

`contract.no_text` also appears as 409 on `POST /api/contracts/{id}/index` when no chunks remain after splitting.

The code list lives in `backend/src/ContractReview.Domain/Errors/ErrorCodes.cs`.
