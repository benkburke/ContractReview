# ContractReview: project decisions

Status: approved 2026-11-02

## Time box
First demo within **90 minutes** (D-001). The thin path is kept to the smallest end-to-end slice: one contract at a time, happy path, honest stubs.

## Product
A user uploads a contract. The system extracts its clauses and flags risky ones, and a reviewer approves or rejects the contract (D-002).
- **Users:** uploader (submits contracts) and reviewer (decides). With no auth these are roles in the UI only, not enforced identities.
- **Key workflows:** upload contract → analyse (extract clauses, flag risks) → review findings → approve / reject.

## Auth
None: no login, no roles, no user accounts (D-003).

## Realtime
Yes, via SignalR (D-004):
- analysis progress pushed to the uploader while extraction and risk flagging run;
- review status and decision changes pushed to everyone viewing that contract.

## Deployment target
Azure App Service + Azure SQL (D-005). Local development uses LocalDB.

## Accessibility
WCAG 2.2 AA (D-006).

## Languages
English only; no i18n library. User-facing text still lives in `src/strings/` (D-007).

## Integrations
None (D-008). Uploaded files are stored in the app database, and clause extraction and risk flagging run in-process with simple rules (no external LLM or parsing API).
