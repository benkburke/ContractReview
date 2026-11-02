# Backend rules (.NET 10)

## Architecture
- Clean/Onion dependencies point inward: Api → Application → Domain; Infrastructure → Application/Domain. Domain has no framework references.
- Data access: Application defines `IAppDbContext` (one `DbSet<T>` per aggregate root plus `SaveChangesAsync`). Application references `Microsoft.EntityFrameworkCore` only for `DbSet<T>`/LINQ, never a provider package. Infrastructure's `AppDbContext` implements it. No generic repositories unless a spec asks for one.
- Minimal API endpoints (and hubs, if realtime) live ONLY in `ContractReview.Api` (`MapGroup` in `Endpoints/<Feature>Endpoints.cs`; hubs in `Hubs/`). Endpoints only translate HTTP to a command or query and back; no business logic.
- Endpoints return `TypedResults` (`Results<Ok<T>, NotFound, ValidationProblem>` unions) for OpenAPI metadata and testability.
- Validation: `builder.Services.AddValidation()` + DataAnnotations on request DTOs for shape (required, length, range); business rules live in the domain. Failures are ProblemDetails 400.
- Errors: `AddProblemDetails()` + `UseExceptionHandler()`; a `DomainExceptionHandler : IExceptionHandler` maps `DomainException` to a 400 or 409 ProblemDetails carrying its error code; never leak exception details.
- Health: `AddHealthChecks()` + `MapHealthChecks(ApiRoutes.Health)` (Playwright waits on it); add a DB check once EF Core exists.

## CQRS (every endpoint; one database)
- Application is organized by feature: `Features/<Feature>/Commands/<Name>/` and `Features/<Feature>/Queries/<Name>/`, each holding the request record, its handler and its result DTO.
- Abstractions in `Application/Abstractions/Messaging`: `ICommand<TResult>`, `ICommandHandler<TCommand, TResult>`, `IQuery<TResult>`, `IQueryHandler<TQuery, TResult>`, each handler with `HandleAsync(request, CancellationToken)`. `AddApplication()` registers every handler by scanning the assembly. Endpoints inject the one handler they need. No mediator library: MediatR v13+ is commercially licensed, and the indirection costs agents extra reading.
- Commands change state through the domain, call `SaveChangesAsync` once (one transaction) and return only an ID or a small result.
- Queries never change state: `AsNoTracking()` and project straight to DTOs with `Select`; never load an aggregate to read it.

## DDD (where the spec marks business rules)
- Aggregates: entities with private setters, created through static factory methods and changed only through methods that enforce their invariants. Other aggregates are referenced by ID only.
- Value objects: `sealed record`s that validate in their constructor (for example Money, Email, a contract number).
- Invariant violations throw `DomainException` with a constant from `Domain/Errors/ErrorCodes.cs`.
- Domain events: aggregates record `IDomainEvent`s; Infrastructure dispatches them in-process to `IDomainEventHandler<T>` after `SaveChangesAsync`.
- Plain reference data or CRUD with no rules: a plain entity with thin command and query handlers. Do not invent aggregates, value objects or events the spec does not need.

## Constants (no magic strings)
- One static class of `public const string` per concern, next to its owner: `Api/Routes/ApiRoutes.cs` (route groups, paths, `Health`, `OpenApiDocument`, `SwaggerPrefix`, `HubsPrefix` and hub paths), `Api/OpenApi/ApiInfo.cs` (document name, title, version), `Api/Authorization/Policies.cs` and `Roles.cs`, `Api/Cors/CorsPolicies.cs`, `Api/Http/HeaderNames.cs`, `Domain/Errors/ErrorCodes.cs` and `ErrorMessages.cs`, `Infrastructure/ConfigKeys.cs` (connection string names; env var names such as `EF_CONNECTION` and `EF_ALLOW_NON_LOCAL`).
- Options classes expose `public const string SectionName` and bind with `AddOptions<T>().BindConfiguration(T.SectionName)`.
- `WithName` takes a constant or `nameof(...)`; DataAnnotations messages use `ErrorMessages` constants.
- Values that differ by environment (URLs, connection strings, feature flags) live in configuration or env vars; code holds only the key.

## OpenAPI (build-time generation)
- `AddOpenApi(ApiInfo.DocumentName)` (3.1 default) with a document transformer that sets title, version and description from `ApiInfo`. Keep `ApiInfo.DocumentName = "v1"`: build-time generation names other documents `openapi_<name>.json`, which would break `gen:api`. `ApiRoutes.OpenApiDocument` must equal the route `MapOpenApi()` serves, `/openapi/v1.json`. Every endpoint has a name (`WithName`) so generated client functions are stable.
- Swagger UI makes the API browsable: reference `Swashbuckle.AspNetCore.SwaggerUI`. When `SwaggerOptions.Enabled` is true (section `Swagger`; true in `appsettings.Development.json`, false otherwise), call `app.MapOpenApi()` and `app.UseSwaggerUI(o => { o.SwaggerEndpoint(ApiRoutes.OpenApiDocument, ApiInfo.Title); o.RoutePrefix = ApiRoutes.SwaggerPrefix; })`, which serves the UI at `/swagger`. Set `launchUrl` to `swagger` in `launchSettings.json`. If auth is on, add the cookie or bearer security scheme in a document transformer so Swagger UI shows an Authorize button. Keep it off outside Development (Microsoft's guidance, to limit information disclosure).
- The Api project references `Microsoft.Extensions.ApiDescription.Server` and sets
  `<OpenApiDocumentsDirectory>$(MSBuildProjectDirectory)/../../openapi</OpenApiDocumentsDirectory>` and
  `<OpenApiGenerateDocumentsOptions>--file-name openapi</OpenApiGenerateDocumentsOptions>`
  so every build writes `backend/openapi/openapi.json`.
- Build-time generation starts the app's entry point. Skip anything that needs a database, secrets or external services when `Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider"`.

## CORS, auth and realtime
- CORS policy `CorsPolicies.Frontend`: `WithOrigins(<frontend URL from configuration, default http://localhost:3000>)`, `AllowAnyHeader()`, `AllowAnyMethod()`; add `AllowCredentials()` only when cookies are used (Identity cookies, or SignalR).
- If auth = ASP.NET Core Identity: Identity API endpoints with cookie auth in the same `AppDbContext`; the frontend calls with `credentials: "include"`; antiforgery on state-changing form posts.
- If auth = external OIDC: `AddAuthentication().AddJwtBearer()` with authority and audience from configuration; if realtime, read the token from the `access_token` query string for paths under `ApiRoutes.HubsPrefix` in `JwtBearerEvents.OnMessageReceived`.
- If auth is on: endpoint groups call `RequireAuthorization()`; anonymous endpoints opt out with `AllowAnonymous()`. Policies are named in the spec.
- If realtime: strongly typed hubs `Hub<I<Hub>Client>`; push via `IHubContext<THub, I<Hub>Client>`; `.RequireCors(CorsPolicies.Frontend)` on `MapHub`.

## Persistence (SQL Server, EF Core code first)
- Only add a database when an approved spec needs stored data. When it does:
  - Entities are plain C# classes in Domain (no EF attributes or package). Mapping lives in Infrastructure as `IEntityTypeConfiguration<T>` classes, loaded with `modelBuilder.ApplyConfigurationsFromAssembly(...)`.
  - Packages: `Microsoft.EntityFrameworkCore.SqlServer` in Infrastructure; `Microsoft.EntityFrameworkCore` in Application; `Microsoft.EntityFrameworkCore.Design` in Api (`PrivateAssets=all`). `dotnet-ef` is a local tool (`dotnet tool restore`).
  - Connection string name `ConfigKeys.AppDbConnection` (`AppDb`). Development value in `appsettings.Development.json`: `Server=(localdb)\\MSSQLLocalDB;Database=ContractReview_Dev;Trusted_Connection=True;TrustServerCertificate=True`. Other environments use env vars or a secret store.
  - Infrastructure has `AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>` for all EF tooling. It uses `EF_CONNECTION` if set, otherwise the LocalDB development string, and throws unless the server is `(localdb)\...` or `EF_ALLOW_NON_LOCAL=1` is set.
  - Never change tables by hand, with SQL scripts, or with `EnsureCreated()`. Change the entity or configuration, then `cd backend && dotnet ef migrations add <PascalCaseName> -p src/ContractReview.Infrastructure -s src/ContractReview.Api -o Migrations`.
  - Check every generated migration; if it drops or renames a column or table, stop and report it (possible data loss).
  - Migrations are append-only once committed; fix mistakes with a new migration.
  - SQL Server specifics: `HasMaxLength` on every string, `HasPrecision` on every decimal, `DateTimeOffset` or UTC `datetime2` for timestamps, a `rowversion` token where the spec mentions concurrent edits.
  - Seed reference data with `HasData` or `UseSeeding`/`UseAsyncSeeding`, never ad-hoc SQL.
- Migrations run during build: the Api `.csproj` imports `../../build/Migrations.targets`, which runs `dotnet ef database update` (LocalDB only, through the factory) after each Debug build. Skipped in Release, in CI (`CI=true` or `TF_BUILD=True`), with `-p:ApplyMigrationsOnBuild=false` (verify passes this), and until the first migration exists. A failed migration fails the build; fix it, never disable the target.

## Config and observability
- Options pattern with `ValidateOnStart()`; secrets via user-secrets/env vars only. Structured `ILogger` logging.

## Tests
- xUnit. Unit tests for Domain/Application never touch a database.
- Integration tests use `WebApplicationFactory<Program>` against LocalDB: ONE assembly-level fixture creates `ContractReview_Test_<guid>`, runs `Database.Migrate()` once (never `EnsureCreated()`), and drops the database on dispose; reset data between tests with Respawn or unique data per test.
- If realtime: hub tests with `HubConnectionBuilder` over the test server.
