# ContractReview: project decisions

Status: approved 2026-11-02, revised 2026-11-02 for the warranty claim scope (specs/challenge-update.md)

## Time box
First demo by **5pm Pacific on 2026-11-02** (D-023, replaces D-001). The thin path is the smallest end-to-end slice: one contract, one claim at a time, happy path, honest stubs.

## Product
A Warranty Claim Analysis Engine (D-015, replaces D-002). A user uploads a warranty contract PDF once, then asks natural-language claims against it (for example "My air conditioner is broken. Is it covered?"). The system retrieves the relevant contract sections and an LLM returns a structured evaluation: coverage status (Covered, Not Covered, Conditional, Insufficient Information), confidence 0–100, covered items, excluded items and reasoning citing the contract text.
- **Users:** a customer or claims agent asking about one contract. No auth, so no enforced identities.
- **Key workflows:** upload contract (stored, D-017) → ask a claim against it → see the evaluation dashboard.
- **Coverage level:** the LLM infers it from the contract; if the contract leaves it blank, the user is asked to choose it (D-020).
- **State amendments:** judged against the base contract on the thin path; state-specific amendments come later (D-021).
- **Second-pass evaluator** (agree/disagree with the answer, Debug/Evaluation panel): in scope as a depth item (D-018).

## Auth
None: no login, no roles, no user accounts (D-003).

## Realtime
Yes, via SignalR (D-019, replaces D-004): claim evaluation progress (for example retrieving, reasoning, done) is pushed to the user who asked.

## Deployment target
Azure App Service + Azure SQL (D-005). Local development uses LocalDB.

## Accessibility
WCAG 2.2 AA (D-006).

## Languages
English only; no i18n library. User-facing text still lives in `src/strings/` (D-007).

## Integrations
OpenRouter (D-016, replaces D-008), OpenAI-compatible API:
- chat model for claim reasoning, with a strict system prompt and structured JSON output;
- embedding model for semantic retrieval; contract chunks and their vectors are computed at upload and stored in SQL Server (D-022).
Model names come from configuration; the API key lives in `dotnet user-secrets` locally and app settings in Azure, never in git. PDF text extraction stays in-process (PdfPig).
