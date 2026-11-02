---
name: fix
description: Lightweight workflow for bug fixes, refactors, dependency updates and small changes that add no endpoint, hub event, schema change, auth change or new user-facing flow.
---
Run the Fix workflow for: $ARGUMENTS (if empty, use the work the human described).
1. Scope check: if the change needs a new or changed endpoint, hub method or event, migration, auth rule or user-facing flow, switch to the Feature workflow.
2. Investigate yourself with Grep/Glob and the failing output the human gave (use `scout` only if it spans many folders). Write `specs/fixes/<yyyy-mm-dd>-<slug>.md` in 10 lines or fewer: problem, root cause or best hypothesis, planned change, owning agent, regression test. Share it in one short message. Wait for the human only if the change touches data, auth, or deletes anything. Record any choice they make as a decision.
3. Bug: dispatch `tester` in fix mode (`TEST MODE: thin` while the thin path is unfinished) for a regression test that fails for the reported reason. Refactor or dependency update: dispatch `tester` to confirm or add coverage of the touched code first.
4. Dispatch the owning agent (`backend` or `frontend`) with the note path and the failing test name. Dependency updates: one package family at a time; read release notes for major versions and list breaking changes in the note.
5. Dispatch `tester` to confirm the regression test passes and run `node scripts/verify.mjs --e2e` once. Failures follow the loop caps in your agent file.
6. Dispatch `documentor` only if setup steps or documented behavior changed.
7. Review gate: summary, verify result and a Conventional Commit message (`fix:`, `refactor:` or `chore(deps):`); wait for approval; commit; suggest `/clear`.
