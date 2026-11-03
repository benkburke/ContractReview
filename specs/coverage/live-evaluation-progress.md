# Coverage: live-evaluation-progress (thin)

| Criterion | Happy | Critical failure | Tests |
|---|---|---|---|
| AC1 steps in order, claimId matches 201 | covered (integration real SignalR client, Vitest, e2e) | n/a | ClaimProgressHubTests.Claim_with_connection_id_pushes_steps..., ClaimProgress.test.tsx, claims.spec.ts "live progress" |
| AC2 Failed step pushed | covered (unit EvaluateClaimProgressTests, Vitest) | n/a | |
| AC3 per-connection isolation | covered (second client receives nothing) | covered | ClaimProgressHubTests |
| AC4 hub unavailable still works | covered (Vitest mocked hub) | deferred (D7 out of scope) | |
| AC5 unknown connection id -> 201 | covered | covered | Claim_with_unknown_connection_id_is_still_201 |
| AC6 validation errors push no steps | covered (400 with connection id) | 404/409 deferred | Invalid_claim_with_connection_id_pushes_no_steps |
| Accessibility (axe while busy) | DEFECT: color-contrast on disabled helper text | | claims.spec.ts "accessible while progress is shown" fails |

Deferred (thin): overlong/blank header variants (>200 chars), 404/409 no-push, concurrent two-browser e2e, Failed-step e2e, reconnect behaviour.
