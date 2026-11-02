# Upload PDF contract
Status: done 2026-11-02
Roadmap: T1 (thin)

## Context
First thin-path step (D-010): a user uploads one text-based PDF contract with a title (PDF only, D-009). The server checks it, extracts the text in-process (no external service, D-008), stores the file and the text in the database, and the user can list contracts and open one to read the extracted text. Clause splitting and risk flags come in T2.

## Sides involved
Both: backend (endpoints, aggregate, PDF extraction, first EF migration) and frontend (list + upload page, detail page).

## Acceptance criteria
1. Given a text-based PDF under the size limit and a title, when the user submits the upload form, then the contract is stored, the user goes to its detail page, and it shows the title, file name, size, page count, upload time and the extracted text.
2. Given stored contracts, when the user opens the contracts page, then they see them newest first with title, file name, page count and upload time, each linking to its detail page.
3. Given a file that is not a PDF (wrong extension, wrong content type or no `%PDF-` header), when uploaded, then the API returns 400 ProblemDetails `contract.file_not_pdf` and the form shows the message next to the file field; nothing is stored.
4. Given a file over the size limit, when uploaded, then the API returns 400 `contract.file_too_large` (413 if the request exceeds the server body limit), and the form shows the message; nothing is stored.
5. Given a missing or blank title, or a title over 200 characters, when submitted, then the form blocks submission client-side and the API also returns 400 `contract.title_invalid`.
6. Given a PDF with no extractable text (scanned/image-only) or a corrupt/encrypted PDF, when uploaded, then the API returns 400 `contract.no_text` (D-013) or `contract.pdf_unreadable`; nothing is stored.
7. Given an unknown id, when the detail page or `GET /api/contracts/{id}` is requested, then 404 ProblemDetails `contract.not_found` and the page shows a "contract not found" message with a link back to the list.

## Endpoints
| Method | Route | Request | Success | Errors |
|---|---|---|---|---|
| POST | `/api/contracts` | `multipart/form-data`: `title` (string), `file` (binary) | 201 `ContractSummaryDto`, `Location` header | 400, 413 |
| GET | `/api/contracts` | – | 200 `ContractSummaryDto[]` (newest first) | – |
| GET | `/api/contracts/{id:guid}` | – | 200 `ContractDetailDto` | 404 |

- `ContractSummaryDto`: `id` (uuid), `title`, `fileName`, `sizeBytes` (int64), `pageCount` (int), `uploadedAtUtc` (date-time).
- `ContractDetailDto`: the summary fields plus `extractedText` (string).
- Error codes are constants in the Domain/Application error catalogue; ProblemDetails carries `code` in extensions.

## Validation rules and limits
- `title`: required, trimmed, 1–200 characters.
- `file`: required; file name ends in `.pdf` (case-insensitive), 1–255 characters; content type `application/pdf`; first bytes `%PDF-`; size 1 byte to **10 MB** (D-012, configurable via options `Upload:MaxFileBytes`); Kestrel/form body limit set slightly above it.
- Extracted text: must be non-empty after trimming; stored as-is (page texts joined by blank lines). No length cap beyond the 10 MB file cap.
- Page count: taken from the PDF; no separate limit.

## Realtime
None in T1 (SignalR starts in T2).

## Auth
None (D-003). Endpoints are anonymous.

## Domain model
- Aggregate `Contract` (factory `Contract.Upload(...)`): invariants — title 1–200 chars, file name ends `.pdf`, size within limit, non-empty content, non-empty extracted text, page count ≥ 1. Throws `DomainException` with the codes above.
- Application: command `UploadContract` (validates, calls `IPdfTextExtractor`, creates the aggregate, saves); queries `ListContracts` (projection without file bytes or text) and `GetContract`.
- Infrastructure: `IPdfTextExtractor` implemented with **UglyToad.PdfPig** (MIT, pure .NET); returns page count and text, or a typed failure for unreadable/encrypted files.
- No domain events needed yet.

## Data model changes
Add EF Core, `IAppDbContext`, `AppDbContext`, `AppDbContextFactory` and the first migration (per bootstrap rule). Table `Contracts`: `Id` uniqueidentifier PK, `Title` nvarchar(200), `FileName` nvarchar(255), `ContentType` nvarchar(100), `SizeBytes` bigint, `PageCount` int, `FileContent` varbinary(max), `ExtractedText` nvarchar(max), `UploadedAtUtc` datetime2; index on `UploadedAtUtc`.

## Frontend
- `/contracts`: upload form (title text field, file input `accept="application/pdf,.pdf"`, submit button with busy state) above the list; empty state when there are no contracts. `/` redirects to `/contracts`.
- `/contracts/[id]`: header with title and metadata, extracted text in a readable, scrollable region preserving line breaks; back link.
- Errors via the shared ProblemDetails handler; field errors mapped to the matching field by `code`.

## Accessibility
Labelled fields; error text tied with `aria-describedby` and announced (`role="alert"`); submit button exposes busy state (`aria-busy`/disabled with progress label); file input reachable and operable by keyboard; list is a semantic list/table; extracted text region is focusable (`tabIndex=0`) with a label so keyboard users can scroll it; axe clean.

## Out of scope (Depth)
DOCX and OCR for scanned PDFs (D1); search, filter, pagination of the list (D4); re-upload/versioning (D5); downloading or previewing the original PDF; deleting contracts; drag-and-drop upload; full failure-scenario test matrix (D6).

## Open questions
None. Size limit 10 MB (D-012); scanned PDFs rejected (D-013).
