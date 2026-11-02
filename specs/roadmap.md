# Roadmap

Status: approved 2026-11-02 (D-010, D-011). Thin path is sized to the 90-minute first demo (D-001).

## Thin path
- [x] T1 Upload PDF contract: upload one text-based PDF with a title (PDF only, no .txt; type and size limit enforced), extract its text in-process, store file and text in the database, list contracts, open one.
- [ ] T2 Analyse PDF text with live progress: split the extracted text into clauses by rule, flag risky clauses using a fixed built-in keyword list, and push progress to the uploader over SignalR.
- [ ] T3 Review decision with live status: reviewer approves or rejects with an optional note, and every open view of that contract updates live over SignalR.

## Depth
- [ ] D1 DOCX upload and scanned (image-only) PDFs via OCR.
- [ ] D2 Configurable risk rules (stored in the database, editable), with severity levels.
- [ ] D3 Per-clause reviewer comments and accept/override of each risk flag.
- [ ] D4 Contract list search, filter by status and pagination.
- [ ] D5 Re-analysis and version history when a contract is re-uploaded.
- [ ] D6 Hardening: full failure-scenario tests across T1–T3 (invalid files, concurrent decisions, dropped SignalR connections).
- [ ] D7 Azure App Service + Azure SQL deployment pipeline (D-005).
