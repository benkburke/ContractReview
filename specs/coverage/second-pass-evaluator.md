# Coverage: second-pass evaluator (D1), mode full

Legend: covered / n/a (not applicable) / deferred / missing.

## Acceptance criteria
| AC | Covered by |
|---|---|
| 1 review runs and is stored/returned | integration `ClaimReviewTests.Post_stores_a_completed_review_and_get_returns_the_same_review`; unit EvaluateClaimProgressTests, ClaimTests |
| 2 collapsed panel, expanded contents | vitest `ClaimReview.test.tsx`; e2e disputed test (expand, axe both states) |
| 3 no review for InsufficientInformation / needsCoverageLevel | integration `Insufficient_information_has_no_review`, `Needs_coverage_level_has_no_review`; vitest null case; unit skip test |
| 4 review failure keeps result, 201, Failed + review.failed | integration `Failing_review_is_201_with_a_Failed_review...`; unit EvaluateClaimProgressTests; vitest failed; e2e unreviewable |
| 5 Reviewing step between Reasoning and Completed | hub test `Claim_with_connection_id_pushes_steps_in_order...` (ClaimProgressHubTests); vitest ClaimProgress |
| 6 disagreement banner + control expands and focuses panel | integration disputed (agreesWithPrimary=false); vitest banner/focus; e2e disputed |
| 7 reload shows stored review | integration (GET equals POST review, for completed, disputed, failed) |

## Categories
| Area | Null/missing | Empty/whitespace | Bounds | Invalid | Not found/conflict | Auth |
|---|---|---|---|---|---|---|
| ClaimReview / Claim.AttachReview, ReviewFailed | n/a (no input) | covered (whitespace explanation, errorCode) | covered (confidence -1/101, explanation 2000/2001, items 20/21, 300/301) | covered (twice, before Evaluated throw) | covered | n/a (D-003) |
| LlmClaimReview.FindViolation (reviewer output) | n/a | covered | covered (min-1, min, max, max+1 for all limits) | covered | n/a | n/a |
| Handler: reviewer failure / invalid output | n/a | n/a | n/a | covered (ChatFailed keeps result; invalid output via FindViolation) | n/a | n/a |
| POST/GET claims `review` field | n/a | n/a | n/a | covered (Review:Enabled=false) | existing claim tests | n/a (D-003) |
| Hub Reviewing step | n/a | n/a | n/a | covered (skipped when no review: unit) | n/a | n/a |

## Gaps
- OpenRouterClaimReviewer wire parsing, retry once on invalid output and timeout are not tested (no HTTP-stub test in this pass). Deferred: ReviewWire.ParseReview/OpenRouterClaimReviewer unit tests with a fake HttpMessageHandler (existing pattern in OpenRouterChatClientTests).
- Review:TimeoutSeconds range 5-300 options validation not tested.
