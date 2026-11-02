# Project: ContractReview
Full-stack app. Backend: .NET 10 (LTS) Minimal API in `backend/`, SQL Server via EF Core code first. Frontend: Next.js 16 App Router + React 19.2 + TypeScript + Material UI v9 in `frontend/`. Stack rules live in `backend/CLAUDE.md` and `frontend/CLAUDE.md` (they load when you touch those folders). Project decisions (auth, realtime, accessibility, languages) live in `docs/project.md`; the coordinator copies the ones that apply into each task. Rules marked "if realtime" or "if auth" apply only when those decisions say so.

## Environment (Windows)
- Windows 11, Claude Code Desktop, shell tool = Git Bash. Use forward slashes in paths and commands.
- Automation is Node scripts (`node scripts/*.mjs`), never `.sh`, `.bat`, or PowerShell-only scripts.
- Line endings are LF everywhere (`.gitattributes`, `.editorconfig`). Never introduce CRLF.
- No `jq`, no `sed -i` on Windows paths. Delete with Node (`node -e "require('fs').rmSync('path',{recursive:true,force:true})"`); `rm -rf` is denied.

## Layout
- `backend/`: `src/ContractReview.{Domain,Application,Infrastructure,Api}`, `tests/ContractReview.{UnitTests,IntegrationTests}`, `build/Migrations.targets`, `openapi/openapi.json` (generated)
- `frontend/`: `src/app`, `src/components/{atoms,molecules,organisms,templates}`, `src/api/generated`, `src/config.ts`, `src/__tests__`, `e2e/`
- `specs/roadmap.md` (thin path, then depth), `specs/<feature>.md`, `specs/fixes/<date>-<slug>.md`, `specs/coverage/<feature>.md`; `docs/`; `DECISIONS.md` (written only by `scripts/decision.mjs`); `scripts/verify.mjs`, `scripts/status.mjs`

## Checks (run from the repo root; `cd` does not persist between tool calls)
- `node scripts/verify.mjs --scope backend --fast` / `--scope frontend --fast`: inner-loop check for one side.
- `node scripts/verify.mjs --e2e`: the full gate, run once per feature. An unchanged tree returns the cached result at once.
- verify prints one line per step and only the relevant lines of a failure; the full log is in `.verify/`. Read a log only when the printed lines are not enough, and then only the part you need.
- Targeted tests while writing them: `cd backend && dotnet test --no-restore --filter "FullyQualifiedName~<Name>"`, `cd frontend && npx vitest run <path>`.
- Regenerate the API client: `cd frontend && npm run gen:api` (commit the generated files).

## Non-negotiables
- Contract first: the backend's OpenAPI document is the HTTP contract; the frontend uses ONLY the generated client in `frontend/src/api/generated`. If realtime: the C# `I<Hub>Client` interface is mirrored in `frontend/src/realtime/contracts.ts`.
- No magic strings: every string with meaning (routes, config and env var names, policy, role and claim names, error codes and messages, header names, cache, query and storage keys, hub methods and groups, user-facing text) is defined once as a constant, or comes from configuration or an env var, and is referenced by name. Allowed literals: the constant definitions themselves, structured log message templates, `nameof()`, test data and test names, and generated code (migrations, the API client). Where each kind lives: `backend/CLAUDE.md` and `frontend/CLAUDE.md`.
- Backend design: DDD where the spec has business rules, CQRS for every endpoint (see `backend/CLAUDE.md`).
- Errors are RFC 7807 ProblemDetails; the frontend shows them through one shared error handler.
- Secrets never in git: `dotnet user-secrets` or env vars; frontend `.env.local`; `.env.example` holds only safe defaults. Never read or print other `.env*` files.
- Database: SQL Server, EF Core code first; local Debug builds apply pending migrations to LocalDB (see `backend/CLAUDE.md`).
- Never invent an auth scheme; follow the decision you were given. Accessibility: WCAG 2.2 AA unless told otherwise.
- No `git push`, no `dotnet ef database update` against non-local databases, no editing or deleting committed migrations without the human's approval.
- Conventional Commits; one logical change per commit; never commit secrets or build output.

## Working efficiently (every agent)
- Find before you read: Grep/Glob first, then read only the lines you need. Skip generated and vendored files (`src/api/generated`, `*.Designer.cs`, `openapi.json`, lockfiles, `bin/`, `obj/`, `.next/`, `node_modules/`) unless the task is about them.
- Do not re-read a file you just wrote, and do not paste file contents, code or logs into messages.
- Use verify for full checks instead of raw build and test commands; its output is already trimmed.
- Finish with the short report your agent file defines (15 lines or fewer).
- If a guard blocks a write, stop and report it with the blocked path. Never work around a guard with Bash (redirects, `node -e`, `npm pkg set`, moving or deleting files). A block means the setup is wrong or the task is outside your folder; either way the coordinator decides.

## Compact instructions
When compacting, keep: the current spec path and its status, decisions made, open blockers, the last verify result, and files changed. Drop tool output, logs and file contents.
