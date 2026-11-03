# Second-pass evaluator
Status: approved 2026-11-02
Roadmap: D1 (depth)

## Context
specs/challenge.md Phase 2, in scope as a depth item per D-018. After the primary evaluation (T3) succeeds, a second LLM call reviews the primary answer against the same retrieved chunks and returns an evaluation object: its own confidence, a definitive covered true/false, whether it agrees with the primary answer and why, and a coverage breakdown (what is covered / not covered). The result is stored with the claim and shown in an expandable **Debug / Evaluation** panel on the results page. Builds on T4: a **Reviewing** progress step is added.

## Sides involved
Both: backend (Evaluation module, schema, DTO) and frontend (Debug/Evaluation panel, extra progress step).

## Acceptance criteria
1. Given the primary evaluation succeeds, when the claim is evaluated, then a second-pass review runs on the same retrieved chunks and its result is stored and returned in `ClaimEvaluationDto.review`.
2. The results page shows a collapsed **Debug / Evaluation** panel; expanded it shows: Agrees / Disagrees (icon + text), reviewer confidence (number + bar), Covered: Yes / No, accuracy explanation, What is covered list, What is NOT covered list, reviewer model.
3. Given the primary status is `InsufficientInformation` or `needsCoverageLevel` is true, then no review runs (`review` is null, panel says "Not reviewed: the primary answer asked for more information").
4. Given the review call fails, times out or returns invalid output (after one retry), then the primary result is still stored and returned with 201; `review` is `{ state: "Failed", errorCode: "review.failed" }` and the panel says the review was unavailable. A review failure never fails the claim.
5. Given the T4 hub is connected, then progress shows **Reviewing the answer** between Reasoning and Done.
6. Given the review state is Completed and `agreesWithPrimary` is false, then the results page shows a warning banner under the status banner ("The second-pass review disagrees with this answer. See Debug / Evaluation.", with a control that expands and scrolls to the panel), in addition to the panel (D-041).
7. Reloading a result shows the stored review (GET unchanged except the new field).

## Endpoints
No new endpoints. `ClaimEvaluationDto` gains `review` (`ClaimReviewDto | null`):
- `ClaimReviewDto`: `state` (`Completed | Failed`), `agreesWithPrimary` (bool?), `isCovered` (bool?), `confidence` (int 0–100?), `accuracyExplanation` (string?), `coveredItems` (string[]), `notCoveredItems` (string[]), `model` (string?), `errorCode` (string?).

## Validation rules and limits
- No user input. LLM output limits (violations = invalid output): confidence 0–100; explanation ≤ 2,000 chars; ≤ 20 items per list, each ≤ 300 chars.
- Options: `Review:Enabled` (default true), `OpenRouter:ReviewModel` (default `anthropic/claude-haiku-4.5`, a different model from the primary for independent verification, D-042), `Review:TimeoutSeconds` (default 45, range 5–300).
- `Llm:Provider = Fake` also selects a deterministic fake reviewer (agrees unless the claim text contains a documented trigger word, so tests can show disagreement).

## Realtime
`ClaimProgressEvent.step` gains `Reviewing` (mirror in `src/realtime/contracts.ts`).

## Auth
None (D-003).

## Domain model
- `ClaimReview` value object on the `Claim` aggregate; `Claim.AttachReview(review)` / `Claim.ReviewFailed(code)` allowed only once the claim is Evaluated and only once.
- Application: `IClaimReviewer` (Evaluation module, `Features/Claims/Evaluation/`), called by `EvaluateClaimHandler` after the primary result; the claim is saved once with both.
- Infrastructure: `OpenRouterClaimReviewer` (strict JSON schema, temperature 0, separate system prompt constant: judge only from the provided chunks and the primary answer), `FakeClaimReviewer`.

## Data model changes
Migration `AddClaimReview`: `Claims` gets `ReviewState` nvarchar(20) null, `ReviewAgrees` bit null, `ReviewIsCovered` bit null, `ReviewConfidence` int null, `ReviewJson` nvarchar(max) null (explanation, lists), `ReviewModel` nvarchar(200) null, `ReviewErrorCode` nvarchar(100) null. Existing rows stay null (shown as "Not reviewed").

## Accessibility
Panel is an MUI `Accordion` with a heading-level summary button and `aria-expanded`; the disagreement warning is an MUI `Alert` (severity warning, icon + text, not `role=alert` on load); agree/disagree and covered values are text, not colour alone; bar has an `aria-label` with the value; axe clean in both states.

## Out of scope
Re-running the review on demand; using disagreement to change the primary status; reviewing old claims retroactively; aggregate agreement metrics (D4).

## Open questions
None. Disagreement shows a warning banner and the panel (D-041); separate reviewer model (D-042).
