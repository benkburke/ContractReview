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

### OpenRouter key and indexing

Each uploaded contract is split into section-tagged chunks and embedded through OpenRouter (D-016, D-027). The API key is a secret: set it with user-secrets and never commit it or put it in `appsettings.json`:

```bash
cd backend/src/ContractReview.Api
dotnet user-secrets set OpenRouter:ApiKey <your-key>
```

Without a key the app still starts. Uploads still succeed, but the contract stays `Failed` until it is retried (D-026). Use **Retry indexing** on the contract page, or `POST /api/contracts/{id}/index`.

Indexing settings, all in `backend/src/ContractReview.Api/appsettings.json` unless noted:

| Key | Default | Notes |
|-----|---------|-------|
| `OpenRouter:BaseUrl` | `https://openrouter.ai/api/v1` | OpenAI-compatible base URL. |
| `OpenRouter:EmbeddingModel` | `openai/text-embedding-3-small` | 1536 dimensions (D-027). |
| `Embeddings:Provider` | `OpenRouter` | `Fake` selects a deterministic local embedder with no network call. Tests, CI and e2e use it (D-028). Do not set it for normal development. |
| `Indexing:BackfillOnStartup` | `true` | On startup, indexes every contract that is not `Indexed`, one at a time. |
| `Indexing:ChunkSize` | `1200` | Characters per chunk, 200 to 8,000. |
| `Indexing:ChunkOverlap` | `150` | Characters shared by consecutive chunks, 0 to half of `ChunkSize`. |
| `Indexing:MaxChunks` | `2000` | Contracts over this fail with `contract.too_many_chunks`. |

Settings can also be overridden with environment variables using `__` for nesting, for example `Embeddings__Provider=Fake`. Options are validated at startup.

### Claim evaluation

A claim is evaluated by an OpenRouter chat model after its contract is indexed (D-016, D-031). The API key is the same `OpenRouter:ApiKey` user-secret used for embeddings; set it as shown above and never put it in `appsettings.json` or `.env.local`. Without a key, claim evaluation returns 502 `claim.evaluation_failed`.

Claim settings, all in `backend/src/ContractReview.Api/appsettings.json` unless noted:

| Key | Default | Notes |
|-----|---------|-------|
| `OpenRouter:ChatModel` | `openai/gpt-4o-mini` | Chat model for claim evaluation (D-031). |
| `Llm:Provider` | `OpenRouter` | `Fake` selects a deterministic local chat client with no network call. Tests, CI and e2e use it (D-028). Do not set it for normal development. |
| `Claims:TopK` | `8` | Chunks retrieved per claim, range 1 to 20. |
| `Claims:MaxContextChars` | `12000` | Prompt budget for retrieved chunks, range 1,000 to 50,000. Lowest-ranked chunks are dropped to fit. |
| `Claims:ChatTimeoutSeconds` | `60` | Timeout per chat request, range 5 to 300. |

Manual demo check (optional): place the sample Allstate VSC PDF in the git-ignored `samples/` folder at the repository root, for example `samples/allstate-vsc.pdf`. Create the folder if it does not exist:

```powershell
New-Item -ItemType Directory -Force samples
```

Do not commit the PDF. Upload it through the app, wait for `Indexed`, then ask "My air conditioner is broken. Is it covered?" and "I have a flat tire".

## Running the apps

Backend API, from `backend/`:

```bash
dotnet run --project src/ContractReview.Api --launch-profile https
```

The `https` profile (`backend/src/ContractReview.Api/Properties/launchSettings.json`) listens on:

- `https://localhost:7080` (Swagger UI and API)
- `http://localhost:5080`

The `http` profile listens on `http://localhost:5080`, which matches the frontend default, so a plain `dotnet run --project src/ContractReview.Api` works with no extra flags.

- Swagger UI: `https://localhost:7080/swagger` (enabled by default in Development).
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

## Azure deployment

`.github/workflows/azure-deploy.yml` lists the required `production` environment secrets and variables. Its migration job uses OIDC to authenticate a managed identity, opens a temporary SQL firewall rule, runs the migration bundle, and removes the rule. Publish profiles authenticate the later app deployments; they do not grant the migration identity Azure access.

### Azure login reports "No subscriptions found"

This error occurs during Azure CLI subscription discovery, before any firewall change or EF Core migration. Supplying `AZURE_SUBSCRIPTION_ID` does not grant access to that subscription.

An Azure administrator must:

1. Verify that `AZURE_CLIENT_ID` is the **client ID** of the intended user-assigned managed identity, `AZURE_TENANT_ID` is its tenant, and `AZURE_SUBSCRIPTION_ID` is the subscription containing the SQL server. The subscription ID can be a secret or variable; if both exist, the secret takes precedence.
2. In the SQL server's **Access control (IAM)**, check access for that identity's **principal/object ID** (not its client ID). Assign **SQL Server Contributor** on the exact SQL server if missing. Check for assignments to an old or recreated identity, or a server in another subscription.
3. Allow Azure RBAC changes to propagate, then retry login. If subscription discovery is still denied, have the administrator approve subscription-level read access for discovery (for example, **Reader**, which also permits reading other resources throughout the subscription). Keep SQL write permissions scoped to the SQL server; do not grant subscription-wide Contributor just to fix login.
4. Rerun the failed deployment after correcting Azure access. A rerun uses the original workflow and commit; dispatch a new deployment after merging workflow changes to test those changes.

Do not bypass this failure with `allow-no-subscriptions`: tenant-only login does not select the target subscription, and the SQL firewall commands still require subscription access. RBAC assignments must be repaired in Azure; a repository commit cannot grant them. See [Azure login without a subscription](https://github.com/Azure/login#login-without-subscription) and [Azure RBAC scope](https://learn.microsoft.com/azure/role-based-access-control/scope-overview).

### Deployment succeeds but the application does not work

- For the frontend's standalone Next.js package, set App Service **Configuration > General settings > Startup Command** to `node server.js`. An empty command can leave the deployed frontend returning HTTP 503.
- The frontend deployment uses `clean: true` and `restart: true` to replace the deployed package and restart the app. Keep uploaded documents and other persistent data outside the frontend deployment directory; its contents are replaced on deployment.
- Open the app's actual **Default domain**. A generated hostname suffix is not a deployment slot; a portal link containing `/slots/<hostname-suffix>` can return "Resource not found" even when the app exists.
- Set the GitHub `production` variable `NEXT_PUBLIC_API_URL` to the complete API origin, including `https://`, without an `/api` suffix. Rebuild and redeploy the frontend after changing it; the value is embedded during the build. A bare hostname is treated as a relative browser URL and sends requests to the frontend instead.
- Configure the API's SQL connection as an App Service connection string named `AppDb` (SQLAzure type), or as the app setting `ConnectionStrings__AppDb`. A connection named after the SQL server is not the key the API reads. Never paste connection-string values into logs or issue reports.

### Production Swagger

The production API enables Swagger through the App Service setting `Swagger__Enabled=true`. It serves Swagger UI at `/swagger` and the OpenAPI document at `/openapi/v1.json`. The setting survives app deployments; restart the API after changing it. Set it to `false` to disable both routes.

This is an explicit production opt-in: the project has no authentication, so the documentation and Swagger's interactive API calls are publicly accessible. The published configuration remains disabled by default.

### Upload succeeds but embedding indexing fails

Set `OpenRouter__ApiKey` on the **API** App Service, not the frontend. After saving, allow the API to restart so the embedding client loads the new setting. A missing key does not prevent the API from starting; indexing fails and the upload remains stored.

With the default `Indexing:BackfillOnStartup=true`, startup retries contracts that are not indexed, failed, or were interrupted while indexing. Refresh the contract after recovery; do not upload it again just to retry indexing. If it still fails, inspect API logs for the missing-key warning or the embedding provider's HTTP status without sharing the key or contract content.

### API app settings

Set these on the **API** App Service (`contractreview-api`) under **Settings > Environment variables**. Never commit their values or put them in `appsettings.json`.

| Setting | Required | Value |
| --- | --- | --- |
| `ConnectionStrings__AppDb` | Yes | SQL connection string for the application database. |
| `ASPNETCORE_ENVIRONMENT` | Yes | `Production` |
| `Cors__FrontendOrigin` | Yes | Frontend origin, such as `https://<frontend-default-domain>`, without a trailing slash. The default in `appsettings.json` is `http://localhost:3000`. |
| `OpenRouter__ApiKey` | Yes | OpenRouter API key. Without it, uploads fail with "The embedding service could not index this contract" (502 `contract.embedding_failed`), and claims fail with 502 `claim.evaluation_failed`. |
| `OpenRouter__EmbeddingModel` | No | Defaults to `openai/text-embedding-3-small`. |
| `OpenRouter__ChatModel` | No | Defaults to `openai/gpt-4o-mini`. |

Never set `Embeddings__Provider` or `Llm__Provider` to `Fake` in production. `Fake` selects the deterministic test clients (D-028).

If an upload or claim returns 502, the reason is only in the server log. Open Azure Portal > `contractreview-api` > **Monitoring > Log stream**. The message "The embedding API key is not configured." means `OpenRouter__ApiKey` is missing.

## Notes

- There is no authentication (decision D-003). Anyone who can reach the API can call it.
- EF Core migrations live in `backend/src/ContractReview.Infrastructure/Migrations`. Local Debug builds apply pending migrations to LocalDB automatically; CI and Release builds do not.
