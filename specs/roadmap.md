# Roadmap

Status: approved 2026-11-02 (D-024; revised for the warranty claim scope, specs/challenge-update.md). Thin path is sized to the 5pm Pacific demo (D-023).

## Thin path
- [x] T1 Upload PDF contract: upload one text-based PDF with a title, extract its text in-process, store file and text, list contracts, open one.
- [x] T2 Section-aware chunks and embeddings: on upload, split the text into chunks tagged with their section (e.g. SCHEDULE OF COVERAGE, WHAT IS NOT COVERED), embed them via OpenRouter and store chunks + vectors; backfill existing contracts; show sections on the contract page.
- [x] T3 Evaluate a claim: ask a claim on a contract, retrieve top chunks by vector similarity, strict-prompt LLM returns a stored ClaimEvaluationResult (status, confidence, covered/excluded items, reasoning with cited clauses); coverage level inferred, else the user is asked; results dashboard (status, confidence gauge, cards). Accept: AC broken and flat tires on the sample PDF.
- [ ] T4 Live evaluation progress: push evaluation steps (retrieving, reasoning, done/failed) to the asking user over SignalR.

## Depth
- [ ] D1 Second-pass evaluator: agree/disagree with reasoning, own confidence and coverage breakdown, shown in a Debug/Evaluation panel (D-018).
- [ ] D2 State amendments: state picker; retrieval includes that state's amendments (D-021).
- [ ] D3 Claim history per contract: list and reopen past claims and results.
- [ ] D4 Evaluation set: scripted sample claims with expected outcomes, run as a regression check.
- [ ] D5 DOCX upload and scanned (image-only) PDFs via OCR.
- [ ] D6 Contract list search, filter and pagination.
- [ ] D7 Hardening: full failure-scenario tests across T1–T4 (LLM timeout, malformed LLM output, rate limits, invalid inputs, dropped SignalR connections).
- [ ] D8 Azure App Service + Azure SQL deployment pipeline (D-005).
- [ ] D9 Re-index all contracts when the embedding model changes (specs/contract-chunks-embeddings.md).

## Dropped with the scope change (D-015)
Rule-based clause risk flags, reviewer approve/reject with live status, configurable risk rules, per-clause reviewer comments, re-upload versioning.
