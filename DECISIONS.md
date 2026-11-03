# Decisions

Recorded by the coordinator with `node scripts/decision.mjs`. Append-only: a changed decision gets a new entry that reverses the old one.

## D-001 Time box for first demo (reversed by D-023)
- Date: 2026-11-02
- Options: 1 week | 2-4 weeks | none | 90 minutes
- Choice: 90 minutes
- Why: First demo must be ready within 90 minutes; thin path sized to fit
- Who: You chose

## D-002 Product scope (reversed by D-015)
- Date: 2026-11-02
- Options: Upload + risk review | Reviewer workflow | Clause comparison
- Choice: Upload + risk review: user uploads a contract, system extracts clauses and flags risks, reviewer approves or rejects
- Why: Chosen core product
- Who: You chose

## D-003 Authentication
- Date: 2026-11-02
- Options: ASP.NET Core Identity | External OIDC | None
- Choice: None (no login, no roles)
- Why: Demo scope
- Who: You chose

## D-004 Realtime (SignalR) (reversed by D-019)
- Date: 2026-11-02
- Options: No | Yes: review status | Yes: analysis progress | Yes: both
- Choice: Yes: review status changes and analysis progress
- Why: Viewers see decisions live; uploader sees analysis progress
- Who: You chose

## D-005 Deployment target
- Date: 2026-11-02
- Options: TBD | Azure App Service + Azure SQL | IIS + SQL Server
- Choice: Azure App Service + Azure SQL
- Why: Chosen hosting
- Who: You chose

## D-006 Accessibility target
- Date: 2026-11-02
- Options: WCAG 2.2 AA | other
- Choice: WCAG 2.2 AA
- Why: Default
- Who: You chose

## D-007 Languages
- Date: 2026-11-02
- Options: English only | multiple with next-intl
- Choice: English only, no i18n library
- Why: Single-language demo
- Who: You chose

## D-008 Integrations (reversed by D-016)
- Date: 2026-11-02
- Options: None | File storage | LLM/doc parsing API | Email
- Choice: None
- Why: No external services; uploads stored in the app database and clause/risk analysis runs in-process (rule-based)
- Who: You chose

## D-009 Contract file format on the thin path
- Date: 2026-11-02
- Options: Plain text (.txt) only | PDF upload and analysis | PDF and DOCX
- Choice: PDF upload and analysis on the thin path; no .txt files
- Why: Human wants the demo to handle real contract PDFs; text extracted in-process (no external service, per D-008); DOCX and OCR deferred to Depth
- Who: You overrode Claude's proposal
- Source: specs/roadmap.md

## D-010 Thin path order (reversed by D-024)
- Date: 2026-11-02
- Options: Upload -> Analyse -> Review | Merge upload and analyse
- Choice: T1 Upload PDF -> T2 Analyse with live progress -> T3 Review decision with live status
- Why: Follows one contract through the core workflow end to end within the 90-minute box
- Who: Claude proposed; you accepted
- Source: specs/roadmap.md

## D-011 Thin path cuts
- Date: 2026-11-02
- Options: Keep in thin path | Defer to Depth
- Choice: Defer: DOCX/OCR, configurable risk rules, per-clause comments, search/filter, versioning, hardening tests, Azure deployment
- Why: Fit the 90-minute first demo; built-in keyword list and text-based PDFs only
- Who: Claude proposed; you accepted
- Source: specs/roadmap.md

## D-012 T1 maximum PDF upload size
- Date: 2026-11-02
- Options: 5 MB | 10 MB | 25 MB
- Choice: 10 MB, configurable via Upload:MaxFileBytes
- Why: Covers typical contract PDFs while keeping database rows small
- Who: Claude proposed; you accepted
- Source: specs/upload-pdf-contract.md

## D-013 T1 handling of PDFs with no extractable text
- Date: 2026-11-02
- Options: Reject with contract.no_text | Store with empty text
- Choice: Reject with 400 contract.no_text, nothing stored
- Why: T2 analysis needs text; OCR is deferred to D1 (D-009)
- Who: Claude proposed; you accepted
- Source: specs/upload-pdf-contract.md

## D-014 Approve T1 Upload PDF contract spec
- Date: 2026-11-02
- Options: Approve | Changes needed
- Choice: Approve
- Why: Spec matches the T1 roadmap scope
- Who: You chose
- Source: specs/upload-pdf-contract.md

## D-015 Product scope
- Date: 2026-11-02
- Options: Upload + risk review with approve/reject | Warranty claim analysis: upload a warranty contract PDF, ask natural-language claims, LLM evaluates coverage
- Choice: Warranty claim analysis per specs/challenge-update.md
- Why: Stakeholder clarification: users ask whether an incident is covered by their warranty
- Who: You chose
- Reverses: D-002
- Source: specs/challenge-update.md

## D-016 LLM integration provider
- Date: 2026-11-02
- Options: None | OpenRouter | Anthropic direct | OpenAI direct | Azure OpenAI
- Choice: OpenRouter (OpenAI-compatible API; model names in config; API key in user-secrets/env vars)
- Why: Named in the challenge update; one key gives access to any model
- Who: You chose
- Reverses: D-008
- Source: specs/challenge-update.md

## D-017 How claims reach a contract
- Date: 2026-11-02
- Options: Upload once, ask many | Both with one-shot POST /api/claims/evaluate | One-shot only
- Choice: Upload once, ask many: stored contracts (T1); POST /api/contracts/{id}/claims; claims and results stored
- Why: Reuses T1 storage and avoids re-parsing the PDF per question
- Who: You chose
- Source: specs/challenge-update.md

## D-018 Second-pass answer evaluator (challenge.md Phase 2)
- Date: 2026-11-02
- Options: Depth item | Thin path | Drop
- Choice: In scope as a depth item
- Why: Thin path ships the structured evaluation first
- Who: You chose
- Source: specs/challenge.md

## D-019 Realtime usage
- Date: 2026-11-02
- Options: Drop SignalR | Progress over SignalR | Stream the answer
- Choice: Keep SignalR for claim evaluation progress (e.g. retrieving, reasoning, done) pushed to the asking user
- Why: Stakeholder chose live progress; old review-status use no longer applies
- Who: You overrode Claude's proposal
- Reverses: D-004
- Source: specs/challenge-update.md

## D-020 Customer coverage level for a claim
- Date: 2026-11-02
- Options: Pick per question | Set once per contract | LLM infers
- Choice: LLM infers the coverage level from the contract; if it is blank, the user is asked to choose it
- Why: Stakeholder wants inference with a fallback to asking
- Who: You overrode Claude's proposal
- Source: specs/challenge-update.md

## D-021 State amendments in the contract
- Date: 2026-11-02
- Options: Defer to depth | Thin path state picker
- Choice: Defer to depth; thin path judges against the base contract and notes state amendments may apply
- Why: Keeps the thin path small
- Who: Claude proposed; you accepted
- Source: specs/challenge-update.md

## D-022 Retrieval method for contract chunks
- Date: 2026-11-02
- Options: BM25 in-process | Embeddings via OpenRouter | Whole contract in prompt
- Choice: Embeddings via OpenRouter; chunks and vectors stored in SQL Server, computed at upload
- Why: Stakeholder preference for semantic search
- Who: You overrode Claude's proposal
- Source: specs/challenge-update.md

## D-023 First demo time box
- Date: 2026-11-02
- Options: 90 minutes | 3 hours | Half a day | 1 day
- Choice: Demo by 5pm Pacific today (2026-11-02)
- Why: Stakeholder deadline after scope change
- Who: You chose
- Reverses: D-001
- Source: specs/challenge-update.md

## D-024 Revised roadmap order and cuts for warranty claim scope
- Date: 2026-11-02
- Options: Approve | Move T4 to depth | Changes
- Choice: Approve: T2 chunks+embeddings, T3 claim evaluation+dashboard, T4 SignalR progress; evaluator, state amendments, history, eval set as depth; risk-review items dropped
- Why: Matches challenge-update.md and the 5pm demo
- Who: You chose
- Reverses: D-010
- Source: specs/roadmap.md

## D-025 Approve revised docs/project.md
- Date: 2026-11-02
- Options: Approve | Changes
- Choice: Approve
- Why: Reflects D-015..D-023
- Who: You chose
- Source: docs/project.md

## D-026 Embedding failure during upload
- Date: 2026-11-02
- Options: Keep contract as Failed with Retry | Reject the upload
- Choice: Keep contract and text stored with indexStatus Failed; Retry indexing button and startup backfill retry
- Why: No data loss; upload stays usable when OpenRouter is down
- Who: Claude proposed; you accepted
- Source: specs/contract-chunks-embeddings.md

## D-027 Embedding model
- Date: 2026-11-02
- Options: openai/text-embedding-3-small | openai/text-embedding-3-large
- Choice: openai/text-embedding-3-small (1536 dims), configurable via OpenRouter:EmbeddingModel
- Why: Cheap and sufficient for single-contract retrieval
- Who: Claude proposed; you accepted
- Source: specs/contract-chunks-embeddings.md

## D-028 Embeddings in tests, CI and e2e
- Date: 2026-11-02
- Options: Deterministic fake embedder via config | Call OpenRouter in e2e
- Choice: Fake embedder selected by Embeddings:Provider=Fake in tests, CI and e2e; OpenRouter by default
- Why: No API key or network in CI; deterministic tests
- Who: Claude proposed; you accepted
- Source: specs/contract-chunks-embeddings.md

## D-029 Status code when re-indexing hits too many chunks or no chunks
- Date: 2026-11-02
- Options: 409 with contract.too_many_chunks / contract.no_text | Add 400 to the contract
- Choice: 409 with contract.too_many_chunks or contract.no_text
- Why: Keeps the approved HTTP contract unchanged
- Who: Claude proposed; you accepted
- Source: specs/contract-chunks-embeddings.md

## D-030 Re-indexing an already indexed contract
- Date: 2026-11-02
- Options: Allowed, old chunks kept on failure | Only Failed/NotIndexed
- Choice: Allowed; a failed run keeps the old chunks
- Why: Needed for atomic chunk replacement (AC7)
- Who: Claude proposed; you accepted
- Source: specs/contract-chunks-embeddings.md

## D-031 Default chat model for claim evaluation
- Date: 2026-11-02
- Options: openai/gpt-4o-mini | openai/gpt-4o
- Choice: openai/gpt-4o-mini, configurable via OpenRouter:ChatModel
- Why: Cheap and supports strict JSON schema output
- Who: Claude proposed; you accepted
- Source: specs/evaluate-claim.md

## D-032 Decisive LLM answer with no valid citation
- Date: 2026-11-02
- Options: Downgrade to InsufficientInformation | Keep status and flag
- Choice: Downgrade to InsufficientInformation
- Why: Strict adherence to the contract text
- Who: Claude proposed; you accepted
- Source: specs/evaluate-claim.md

## D-033 Store failed claim evaluations
- Date: 2026-11-02
- Options: Store with state Failed and error code | Store successes only
- Choice: Store with state Failed and error code
- Why: Lifecycle for T4 progress and a debug trail
- Who: Claude proposed; you accepted
- Source: specs/evaluate-claim.md

## D-034 Where the sample Allstate PDF lives for the demo check
- Date: 2026-11-02
- Options: User uploads it manually | Git-ignored samples/ folder
- Choice: Git-ignored samples/ folder
- Why: Available locally for the demo check without committing it
- Who: You chose
- Source: specs/evaluate-claim.md

## D-035 How should claim evaluation handle a model answer that sets needsCoverageLevel=true with no coverage level options?
- Date: 2026-11-02
- Options: Normalize to needsCoverageLevel=false, keep the answer, tighten the prompt, and log the violation reason | Keep rejecting and only add logging
- Choice: Normalize to needsCoverageLevel=false, keep the answer, tighten the prompt, and log the violation reason
- Why: The answer is otherwise valid; rejecting it fails every evaluation of contracts whose excerpts name no tiers, and the retry at temperature 0 repeats the same output
- Who: Claude proposed; you accepted
- Source: specs/fixes/2026-11-02-needs-level-without-options.md

## D-036 How does the user answer a claim follow-up question?
- Date: 2026-11-02
- Options: Append answer to claim text and re-evaluate | Also send answer as coverageLevel when about tiers | Full feature with new endpoint
- Choice: Free-text answer box; re-evaluate as a new claim with text = original claim + question + answer, capped at ClaimRules.MaxTextLength; existing POST endpoint
- Why: Works for every follow-up type without guessing intent and needs no contract change
- Who: Claude proposed; you accepted
- Source: specs/fixes/2026-11-02-answer-follow-up.md

## D-037 How to fix 'No subscriptions found' in the Azure migrate job?
- Date: 2026-11-02
- Options: Workflow change (allow-no-subscriptions or az rest) | Keep PR #3 diagnostics and repair RBAC/IDs in Azure
- Choice: Keep PR #3 workflow and docs; human repairs the identity's role assignment and IDs in Azure
- Why: az CLI validates --subscription against discovered subscriptions; the failure is an Azure access problem a commit cannot fix
- Who: You chose

## D-038 Is roadmap item D8 (Azure App Service + Azure SQL deployment pipeline) complete?
- Date: 2026-11-02
- Options: Complete | Still open
- Choice: Complete: azure-deploy.yml deploys and the human confirmed the deploy works
- Why: Human confirmed the Azure deploy is working
- Who: You chose
- Source: specs/roadmap.md

## D-039 What to work on next?
- Date: 2026-11-02
- Options: T4 only | T4 then D1 and D3 | T4 then depth in roadmap order
- Choice: T4, then D1 (second-pass evaluator) and D3 (claim history), ahead of D2
- Why: Human prioritized these depth items
- Who: You chose
- Source: specs/roadmap.md

## D-040 T4 live progress design
- Date: 2026-11-02
- Options: A: synchronous POST, push to caller's connection id | B: 202 + background evaluation + claim group
- Choice: A: synchronous POST, push to the caller's SignalR connection id
- Why: Smallest change; evaluation works without the hub
- Who: Claude proposed; you accepted
- Source: specs/live-evaluation-progress.md

## D-041 Where to show second-pass reviewer disagreement
- Date: 2026-11-02
- Options: Debug panel only | Warning banner on results plus Debug panel
- Choice: Warning banner on the results page and details in the Debug / Evaluation panel
- Why: Disagreement should be visible without expanding the panel
- Who: You chose
- Source: specs/second-pass-evaluator.md

## D-042 Second-pass reviewer model
- Date: 2026-11-02
- Options: Same as primary (openai/gpt-4o-mini) | Separate model (e.g. anthropic/claude-haiku-4.5)
- Choice: Separate model for independent verification; default OpenRouter:ReviewModel = anthropic/claude-haiku-4.5
- Why: Independent verification
- Who: You chose
- Source: specs/second-pass-evaluator.md
