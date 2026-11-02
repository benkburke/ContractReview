# Setup and run (Windows)

ContractReview is a Next.js frontend (`frontend/`) and an ASP.NET Core 10 API (`backend/`). This guide takes a fresh Windows clone to running apps and passing checks.

## Prerequisites

| Tool | Notes |
|------|-------|
| Git for Windows | Required. Use Git Bash or PowerShell. |
| Node.js LTS | Includes npm. |
| .NET 10 SDK | `dotnet --list-sdks` should show `10.0.`. |
| SQL Server LocalDB | Ships with Visual Studio (SQL Server data tools workload) or the SQL Server Express installer. The instance is `MSSQLLocalDB`. |

## One-time machine setup

Run in PowerShell or Git Bash:

```bash
git config --global core.autocrlf false
git config --global core.longpaths true
dotnet dev-certs https --trust
sqllocaldb info
```

`sqllocaldb info` must list `MSSQLLocalDB`. If it does not, install LocalDB and run `sqllocaldb start MSSQLLocalDB`.

## First run

From the repository root:

```bash
cd backend
dotnet tool restore

cd ../frontend
npm ci
npx playwright install chromium
cp .env.example .env.local
```

`.env.local` sets `NEXT_PUBLIC_API_URL`, which `.env.example` defaults to `http://localhost:5080`. The API's `http` profile listens on that URL. If you run the API on a different URL, set `NEXT_PUBLIC_API_URL` to match.

A Debug build of the API creates and migrates the `ContractReview_Dev` LocalDB database. The first EF Core migration, `CreateContracts`, is applied automatically to LocalDB on each Debug build. LocalDB must be running (`sqllocaldb start MSSQLLocalDB`) for the build and the API to reach it.

Upload size limit: `Upload:MaxFileBytes` (in `backend/src/ContractReview.Api/appsettings.json`) sets the maximum PDF size in bytes. The default is `10485760` (10 MB, D-012). Kestrel's request body limit is that value plus 1 MiB, so oversized requests can be rejected with 413 before the file is read. Override it with user-secrets or an environment variable (`Upload__MaxFileBytes`) if needed.

## Running the apps

Backend API, from `backend/`:

```bash
dotnet run --project src/ContractReview.Api --launch-profile https
```

The `https` profile (`backend/src/ContractReview.Api/Properties/launchSettings.json`) listens on:

- `https://localhost:7080` (Swagger UI and API)
- `http://localhost:5080`

The `http` profile listens on `http://localhost:5080`, which matches the frontend default, so a plain `dotnet run --project src/ContractReview.Api` works with no extra flags.

- Swagger UI: `https://localhost:7080/swagger` (Development only).
- Health: `/health` on the API host, for example `https://localhost:7080/health`. It includes a database check, so LocalDB must be running or the endpoint reports unhealthy.
- API reference: `docs/api.md`.

Frontend, from `frontend/`:

```bash
npm run dev
```

Open `http://localhost:3000`.

The API allows CORS from `Cors:FrontendOrigin` (default `http://localhost:3000`, set in `backend/src/ContractReview.Api/appsettings.json`). Change that key only if the frontend runs on another origin.

## Checks

Run from the repository root:

```bash
node scripts/verify.mjs --scope backend --fast
node scripts/verify.mjs --scope frontend --fast
node scripts/verify.mjs --e2e
```

- `--scope backend|frontend` runs one side. Without it, both sides run plus the API client drift check.
- `--fast` skips the format check and the production frontend build.
- `--e2e` adds the Playwright end-to-end tests. This is the full gate and what CI runs.

Each step writes its full output to `.verify/<step>.log`. A passing run is recorded in `.verify/result.json`, so an identical rerun returns immediately.

Regenerate the typed API client after changing endpoints (from `frontend/`):

```bash
npm run gen:api
```

## CI

`.github/workflows/ci.yml` runs the full gate (`node scripts/verify.mjs --e2e`) on `windows-latest` for pushes to `main` and for pull requests. On failure it uploads `.verify/` as the `verify-logs` artifact.

## Notes

- There is no authentication (decision D-003). Anyone who can reach the API can call it.
- EF Core migrations live in `backend/src/ContractReview.Infrastructure/Migrations`. Local Debug builds apply pending migrations to LocalDB automatically; CI and Release builds do not.
