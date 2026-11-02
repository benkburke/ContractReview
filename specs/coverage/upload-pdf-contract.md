# Coverage: upload-pdf-contract (thin mode)

Backend integration: `backend/tests/ContractReview.IntegrationTests/ContractEndpointsTests.cs` (LocalDB test DB via `ApiFactory`, `TestPdfs` builds PDFs with PdfPig). Domain unit tests: `UnitTests/Domain/ContractTests.cs` (pre-existing). Unit: `frontend/src/__tests__/{problem,uploadValidation}.test.ts` (pre-existing). E2E: `frontend/e2e/contracts.spec.ts`.

| AC / endpoint | Happy | Missing/null | Empty/blank | Bounds | Invalid input | Not found | Auth |
|---|---|---|---|---|---|---|---|
| 1 POST upload + GET detail | integration + e2e | covered (missing title, missing file) | covered (blank title) | covered (10 MB exactly accepted, title 200) | covered | n/a | n/a (D-003) |
| 2 GET list newest first | integration + e2e | n/a | deferred (empty state UI) | deferred (paging out of scope) | n/a | n/a | n/a |
| 3 not a PDF | - | - | - | - | covered: extension, content type, header (integration); e2e form message | n/a | n/a |
| 4 too large | - | - | - | covered: 10 MB+1 -> 400 `contract.file_too_large` | - | n/a | n/a |
| 5 title invalid | - | covered | covered | covered: 200 ok, 201 -> 400 | whitespace/Unicode variants deferred | n/a | n/a |
| 6 no text / unreadable | - | - | - | - | covered: image-only, corrupt (integration); image-only (e2e). Encrypted PDF deferred | n/a | n/a |
| 7 not found | - | - | - | - | covered (non-GUID id -> 404) | covered integration + e2e | n/a |

## Gaps / deferred
- 413 for a request over the Kestrel body limit: not testable through `TestServer` (Kestrel limits not enforced; the form limit yields 400). Needs a real-Kestrel test; deferred.
- Encrypted PDF fixture, Unicode/whitespace title variants, empty-file (0 byte) upload, empty list state, network-failure UI: deferred (thin).
- Frontend component tests for the form/list/detail: deferred; e2e covers the flows.
- Axe: run on `/contracts` (before and after upload), detail page and not-found page.

## Defects found
- Resolved in fix round 1 (2026-11-02): build failure on /contracts/[id], axe aria-progressbar-name and color-contrast, e2e DB migration ordering. verify --e2e PASS.
