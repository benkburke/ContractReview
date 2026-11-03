# Claim history per contract
Status: done 2026-11-02
Roadmap: D3 (depth)

## Context
Claims are already stored (T3, D-033 stores failed ones too) and each result has a reloadable URL, but there is no way to find past claims. This adds a newest-first list of a contract's claims on the contract page; each row opens the stored result.

## Sides involved
Both: backend (list query + endpoint) and frontend (Claim history section on the contract page).

## Acceptance criteria
1. Given a contract with claims, when the user opens the contract page, then a **Claim history** section lists its claims newest first: claim text (truncated to 120 chars, full text in the row's accessible name), status (icon + text; "Failed" for failed claims), confidence (when evaluated), created date/time.
2. When the user activates a row, then the results page `/contracts/{id}/claims/{claimId}` opens.
3. Given no claims, then the section shows "No claims yet."
4. Given more than one page, then **Load more** fetches the next page and appends it.
5. Given a new claim was just evaluated, when the user goes back to the contract, then it is at the top of the list.
6. Given an unknown contract: 404 `contract.not_found`. Given invalid paging values: 400 `paging.invalid`.

## Endpoints
| Method | Route | Query | Success | Errors |
|---|---|---|---|---|
| GET | `/api/contracts/{id:guid}/claims` | `page` (int, default 1), `pageSize` (int, default 20) | 200 `ClaimHistoryPageDto` | 400, 404 |

- `ClaimHistoryPageDto`: `items` (`ClaimSummaryDto[]`), `page`, `pageSize`, `totalCount`.
- `ClaimSummaryDto`: `id`, `claimText` (full, ≤ 2,000), `state` (`Evaluated | Failed`), `status` (nullable enum), `confidence` (nullable int), `reviewAgrees` (nullable bool, populated once D1 exists, else null), `createdAtUtc`.
- Ordering: `createdAtUtc` desc, then `id` desc (stable). Uses the existing (`ContractId`, `CreatedAtUtc`) index.

## Validation rules and limits
- `page`: 1–10,000; `pageSize`: 1–100. Out of range → 400 `paging.invalid` (constant). A non-integer value is rejected by ASP.NET model binding with a plain 400 (no `paging.invalid` code); the frontend never sends one.

## Realtime
None.

## Auth
None (D-003). Anyone can see a contract's claim history, consistent with contracts being public in the app today.

## Domain model
Plain read query: `ListClaims(contractId, page, pageSize)` → `ClaimHistoryPageDto` (AsNoTracking, projects only summary columns; never loads `ResultJson`).

## Data model changes
None.

## Frontend
- Contract page: **Claim history** section below the Ask a claim form, rendered with an MUI `List` of link rows (or a table on wide screens). Uses the generated client only. Strings in `src/strings/`, routes in `src/constants/`.

## Accessibility
Section has a heading; each row is a single link whose name includes text, status and date; status is icon + text; **Load more** is a button that moves focus to the first newly loaded row and announces the count via `role="status"`; axe clean.

## Out of scope
Deleting claims; search/filter of claims; history across all contracts; editing or re-running a past claim (Evaluate again stays on the results page).

## Open questions
None.
