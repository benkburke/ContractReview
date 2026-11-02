# Coverage: evaluate-claim (T3, thin mode)

Backend: `backend/tests/ContractReview.IntegrationTests/ClaimEndpointsTests.cs` (existing smoke), `ClaimEvaluationScenariosTests.cs` (new); unit `ClaimTests`, `ClaimRetrieverTests` (existing). Vitest: `frontend/src/__tests__/ClaimFlow.test.tsx`. E2E: `frontend/e2e/claims.spec.ts`. AC9 is manual (real model), skipped.

## Acceptance criteria
| AC | Happy | Critical failure | Notes |
|---|---|---|---|
| 1 ask -> 201 -> dashboard | covered (integration, Vitest form, e2e) | covered (502 form error, 400 field error) | |
| 2 dashboard content | covered (Vitest, e2e) | n/a | status, gauge number, level, reasoning, items, citations |
| 3 vague claim | covered (integration, Vitest follow-up) | n/a | |
| 4 needsCoverageLevel + Evaluate again | covered (integration both calls, Vitest picker/button/payload) | n/a | e2e deferred |
| 5 citations / downgrade | covered (stub chat: invalid labels downgrade for Covered/NotCovered/Conditional; valid label kept, invalid dropped) | covered | |
| 6 404 / 409 | covered (404 contract/claim, 409 not indexed, 409 model mismatch) | covered | |
| 7 502, Failed stored | covered (throwing chat: 502 code, Failed claim persisted with error code and no result, GET returns Failed; malformed output 502; Vitest Failed view) | covered | |
| 8 GET stored claim | covered (integration Location fetch, Vitest not-found) | covered | |

## Endpoints
| Endpoint | Happy | Null/missing | Empty/whitespace | Bounds | Invalid input | 404/409/502 | Auth |
|---|---|---|---|---|---|---|---|
| POST claims | covered | covered ({}, null) | covered (blank text, blank level) | text 3 chars and 2001 chars covered; 5, 2000 exact: deferred | covered | covered | n/a (D-003) |
| GET claim | covered | n/a | n/a | n/a | non-GUID id: deferred | covered | n/a |

## Deferred (thin mode)
- Boundary table for text 5/2000 and level 100/101; Unicode and whitespace variants.
- OpenRouterChatClient HTTP behavior (retries, timeout, missing key, schema output) with a stub handler; Claims options validation at startup.
- FakeChatClient rule unit tests; MaxContextChars truncation; citation excerpt 500-char cap; output-limit violations (each FindViolation branch).
- e2e for needsCoverageLevel, Failed claim, vague claim; claim form not-Indexed message component test.
- Non-GUID route ids.
