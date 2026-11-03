# Coverage: claim-history (D3), mode full

Legend: C covered, N/A not applicable. Backend = backend/tests/ContractReview.IntegrationTests/ClaimHistoryTests.cs; Vitest = frontend/src/__tests__/ClaimHistory.test.tsx; E2E = frontend/e2e/claim-history.spec.ts.

| Item | Happy | Null/missing | Empty/whitespace | Out of bounds | Invalid input | Not found/conflict | Auth |
|---|---|---|---|---|---|---|---|
| AC1 list newest first, row content | C (Backend, Vitest, E2E) | N/A | C (empty history) | C (truncation 120, Vitest) | C (Failed state, null status/confidence) | N/A | N/A (D-003) |
| AC2 row opens result | C (Vitest href, E2E) | N/A | N/A | N/A | N/A | N/A | N/A |
| AC3 "No claims yet." | C (Vitest, E2E, Backend empty) | N/A | C | N/A | N/A | N/A | N/A |
| AC4 Load more appends, focus, status announce | C (Vitest, E2E with 23 claims) | N/A | N/A | C (page past end, Backend) | N/A | N/A | N/A |
| AC5 new claim on top | C (E2E, Backend order) | N/A | N/A | N/A | N/A | N/A | N/A |
| AC6 404 / 400 | C | C (defaults when omitted) | N/A | C page 0,1,10000,10001,-1,int.Max; pageSize 0,1,100,101,-1 | C non-integer page/pageSize: 400 only (ASP.NET plain 400, known) | C unknown contract 404 | N/A |
| GET /api/contracts/{id}/claims ordering | C | N/A | N/A | C ties on createdAtUtc by id desc (SQL Server GUID order, via SqlGuid) | N/A | N/A | N/A |
| reviewAgrees | C true, false, null (unreviewable) | N/A | N/A | N/A | N/A | N/A | N/A |
| Accessibility | axe on claim-history section in all three e2e flows | | | | | | |

Deferred: none. Notes: non-integer page does not return `paging.invalid` (known behaviour, not a defect). Non-GUID contract id route not tested (route constraint, not part of this feature).
