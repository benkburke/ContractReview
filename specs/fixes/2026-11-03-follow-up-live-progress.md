# Follow-up flows show no live progress
- Problem: on the results page, answering a follow-up question and "Evaluate again" (coverage-level picker) show only a busy state; the SignalR evaluation timeline (T4, D1 Reviewing step) never appears.
- Root cause: `ClaimResultView` calls `evaluateClaim(contractId, text, level)` without a connection id and does not use `useClaimProgress` / `ClaimProgressSteps`; only `AskClaimForm` does.
- Planned change: in `ClaimResultView`, use `useClaimProgress(contractId)`, pass its connection id to `evaluateClaim` in both handlers, and render `ClaimProgressSteps` (same live region, same 4 steps, Failed state) under each submit while busy. Hub failure must not block submit (T4 AC4). No API or hub change.
- Owner: frontend. Spec refs: specs/live-evaluation-progress.md, specs/second-pass-evaluator.md (D-040, D-041).
- Regression test: Vitest on ClaimResultView with a mocked hub: submitting a follow-up answer and Evaluate again each send the connection id and show the progress steps; hub down still submits.
