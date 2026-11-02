# Decisions

Recorded by the coordinator with `node scripts/decision.mjs`. Append-only: a changed decision gets a new entry that reverses the old one.

## D-001 Time box for first demo
- Date: 2026-11-02
- Options: 1 week | 2-4 weeks | none | 90 minutes
- Choice: 90 minutes
- Why: First demo must be ready within 90 minutes; thin path sized to fit
- Who: You chose

## D-002 Product scope
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

## D-004 Realtime (SignalR)
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

## D-008 Integrations
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

## D-010 Thin path order
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
