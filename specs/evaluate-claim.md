# Evaluate a claim
Status: done 2026-11-02
Roadmap: T3 (thin)

## Context
Third thin-path step. A user asks a natural-language claim against one indexed contract (D-017: upload once, ask many). The backend embeds the claim, retrieves the top contract chunks by cosine similarity (in-process, D-022), and an OpenRouter chat model (D-016) with a strict system prompt returns a structured `ClaimEvaluationResult`. The claim and its result are stored. The LLM infers the coverage level from the contract; if the contract leaves it blank, the user is asked to pick one and the claim is evaluated again (D-020). State amendments are ignored; the reasoning may note that they can apply (D-021). The request is synchronous; live progress is T4.

## Sides involved
Both: backend (claim aggregate, retrieval, chat client, endpoints, schema) and frontend (claim form on the contract page, results dashboard).

## Acceptance criteria
1. Given an Indexed contract, when the user submits a claim, then the claim is embedded, the top K chunks are retrieved, the LLM evaluates it, the claim and result are stored, the response is 201 and the browser opens the results dashboard.
2. The dashboard shows the coverage status prominently (icon + text), a circular confidence gauge (0–100, with the number as text), the inferred coverage level, and cards for Reasoning, Covered items, Excluded items and Cited clauses (section heading + excerpt).
3. Given a vague claim (e.g. "My car sounds weird"), then status is `InsufficientInformation` and the dashboard shows the LLM's follow-up question asking for diagnostic details.
4. Given a contract whose coverage level is blank and no level was supplied, then the result has `needsCoverageLevel = true` with the tier names found in the contract; the dashboard shows a coverage-level picker and **Evaluate again**, which submits a new claim with the same text and the chosen level and opens its result.
5. Given the LLM's answer cites chunks, then only chunks that were actually retrieved are kept as citations. A `Covered`, `NotCovered` or `Conditional` answer with no valid citation is stored as `InsufficientInformation` (strict adherence to the contract text, D-032).
6. Given an unknown contract id: 404 `contract.not_found`. Given a contract that is not Indexed: 409 `contract.not_indexed`. Given a contract indexed with a different embedding model than the current one: 409 `contract.embedding_model_mismatch`.
7. Given the embedding or chat call fails, times out, or returns output that does not match the schema (after one retry), then 502 `claim.evaluation_failed`, the claim is stored with state `Failed` and the error code (D-033), and the form shows the error. No partial result is ever stored.
8. Given a stored claim, when `GET /api/contracts/{id}/claims/{claimId}` is called, then the stored result is returned (the dashboard URL can be reloaded).
9. Manual demo acceptance on the sample Allstate VSC PDF (`samples/`, git-ignored, D-034) with the real models: "My air conditioner is broken. Is it covered?" returns Covered or Conditional citing the coverage schedule; "I have a flat tire" returns Not Covered citing the exclusions.

## Endpoints
| Method | Route | Request | Success | Errors |
|---|---|---|---|---|
| POST | `/api/contracts/{id:guid}/claims` | `EvaluateClaimRequest` (JSON) | 201 `ClaimEvaluationDto`, `Location` header | 400 validation, 404, 409, 502 |
| GET | `/api/contracts/{id:guid}/claims/{claimId:guid}` | – | 200 `ClaimEvaluationDto` | 404 `contract.not_found` / `claim.not_found` |

- `EvaluateClaimRequest`: `claimText` (string), `coverageLevel` (string, optional).
- `ClaimEvaluationDto`: `id`, `contractId`, `claimText`, `coverageLevelRequested` (nullable), `state` (`Evaluated | Failed`), `createdAtUtc`, `result` (`ClaimEvaluationResultDto`, null when Failed), `errorCode` (nullable).
- `ClaimEvaluationResultDto`: `status` (`Covered | NotCovered | Conditional | InsufficientInformation`), `confidence` (int 0–100), `coverageLevel` (nullable string: the level used or inferred), `needsCoverageLevel` (bool), `coverageLevelOptions` (string[]), `coveredItems` (string[]), `excludedItems` (string[]), `reasoning` (string), `followUpQuestion` (nullable string), `citations` (`ClaimCitationDto[]`), `model` (string).
- `ClaimCitationDto`: `sectionHeading` (string), `excerpt` (string, from the stored chunk text, ≤ 500 chars).
- Chunk vectors and the raw prompt are never returned.

## Validation rules and limits
- `claimText`: required; trimmed length 5–2,000 chars → else 400 `claim.text_invalid`.
- `coverageLevel`: optional; when present, trimmed length 1–100 → else 400 `claim.coverage_level_invalid`. Not checked against the options list (the LLM judges it).
- Route ids: GUIDs; unknown → 404.
- Options, validated at startup: `Claims:TopK` (default 8, range 1–20), `Claims:MaxContextChars` (default 12,000, range 1,000–50,000; lowest-ranked chunks dropped to fit), `Claims:ChatTimeoutSeconds` (default 60, range 5–300).
- LLM output limits (enforced server-side; violations count as malformed output): status in the enum; confidence 0–100; at most 20 covered items, 20 excluded items, 10 citations, 10 coverage-level options; each item ≤ 300 chars; reasoning ≤ 4,000 chars; follow-up question ≤ 500 chars.

## Retrieval and reasoning
- Embed the claim text (plus the coverage level when given) with the existing `IEmbeddingClient`; cosine similarity against every chunk of the contract, in-process; take the top K.
- `IChatCompletionClient` (Application) → `OpenRouterChatClient` (Infrastructure): OpenAI-compatible `POST {BaseUrl}/chat/completions`, temperature 0, `response_format` JSON schema (strict), timeout from options, the same retry policy as embeddings (2 retries on 429/5xx). Option `OpenRouter:ChatModel` (default `openai/gpt-4o-mini`, D-031). Missing API key → 502 `claim.evaluation_failed`.
- Chunks go in the prompt labelled `[C1]..[CK]` with their section heading; the LLM cites by label.
- System prompt (a constant, in Infrastructure) instructs the model to: use only the provided contract text; deny coverage when the part is not explicitly listed for the coverage level; deny when an exclusion applies (e.g. maintenance neglect, waiting period); answer `InsufficientInformation` with a follow-up question when the claim is too vague; infer the coverage level from the text, and when it is blank and none was given, set `needsCoverageLevel` and list the tier names; never invent clauses; note that state amendments may apply.
- **Tests, CI and e2e** (like D-028): `Llm:Provider = Fake` selects a deterministic chat client that derives its answer from the retrieved chunks and the claim text and can produce all four statuses and the coverage-level question from test PDFs; its rules are documented in the class. Playwright's backend env sets `Llm__Provider=Fake`.

## Realtime
None in T3 (T4 pushes progress). The claim's `state` field leaves room for T4's steps.

## Auth
None (D-003). Endpoints are anonymous.

## Domain model
- `Claim` aggregate (new): `Id`, `ContractId`, `ClaimText`, `CoverageLevelRequested`, `State` (`Evaluating → Evaluated | Failed`), `ErrorCode`, `CreatedAtUtc`, `CompletedAtUtc`, and a `ClaimEvaluationResult` value object (fields as the DTO; covered/excluded items, options and citations stored as JSON) plus `Model`.
- Invariants: `Complete(result)` and `Fail(code)` only from `Evaluating`; a result requires confidence 0–100 and a valid status; a decisive status (`Covered | NotCovered | Conditional`) requires ≥ 1 citation, otherwise the factory downgrades it to `InsufficientInformation` (AC5); `needsCoverageLevel` requires ≥ 1 option.
- Application: command `EvaluateClaim(contractId, claimText, coverageLevel)`; query `GetClaim(contractId, claimId)`; `IClaimRetriever` (pure cosine ranking, testable without a DB).
- Infrastructure: `OpenRouterChatClient`, `FakeChatClient`, `ClaimConfiguration`.

## Data model changes
Migration `AddClaims`: new `Claims` table: `Id` uniqueidentifier PK, `ContractId` FK → Contracts (cascade delete), `ClaimText` nvarchar(2000), `CoverageLevelRequested` nvarchar(100) null, `State` nvarchar(20), `ErrorCode` nvarchar(100) null, `CreatedAtUtc` datetime2, `CompletedAtUtc` datetime2 null, `Status` nvarchar(30) null, `Confidence` int null, `CoverageLevel` nvarchar(100) null, `NeedsCoverageLevel` bit not null default 0, `ResultJson` nvarchar(max) null (items, options, citations, reasoning, follow-up), `Model` nvarchar(200) null; index on (`ContractId`, `CreatedAtUtc`).

## Frontend
- Contract page, when Indexed: an **Ask a claim** form (multiline text field with a character counter, optional coverage-level text field, **Evaluate** button with busy state; note that evaluation can take up to a minute). On 201, navigate to `/contracts/{id}/claims/{claimId}`. Errors via the shared ProblemDetails handler. Hidden with a short message when not Indexed.
- Results page `/contracts/[id]/claims/[claimId]`: claim text, status banner (icon + text + colour, never colour alone), MUI `CircularProgress` determinate gauge with the number and "confidence" as text, coverage level, cards for Reasoning, Covered items, Excluded items, Cited clauses; follow-up question when present; coverage-level picker + **Evaluate again** when `needsCoverageLevel`; a Failed claim shows its error and a link back. Back link to the contract.
- Text in `src/strings/`, error codes in `src/constants/errorCodes.ts`, routes in `src/constants/`.

## Accessibility
Form fields labelled, errors linked with `aria-describedby`; Evaluate button exposes busy state and the wait is announced via `role="status"`; status banner and gauge have text equivalents (gauge `aria-label` with the value); cards use headings in order; the picker is a labelled radio group or select; keyboard operable; axe clean.

## Out of scope (Depth)
Live progress (T4); second-pass evaluator (D1); state amendments (D2); claim history list per contract (D3); evaluation set (D4); streaming responses; hybrid/BM25 retrieval; native vector search; full failure-scenario tests (D7).

## Open questions
None. Chat model openai/gpt-4o-mini (D-031); decisive answers without a valid citation are downgraded (D-032); failed claims are stored as Failed (D-033); the sample PDF lives in a git-ignored samples/ folder (D-034).
