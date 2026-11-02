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
