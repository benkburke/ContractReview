import { execFileSync } from "node:child_process";
import { existsSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const E2E_DB =
  "Server=(localdb)\\MSSQLLocalDB;Database=ContractReview_E2E;Trusted_Connection=True;TrustServerCertificate=True";
const here = path.dirname(fileURLToPath(import.meta.url));
const BACKEND_DIR = path.resolve(here, "../../backend");
const MIGRATIONS_DIR = path.join(BACKEND_DIR, "src/ContractReview.Infrastructure/Migrations");

export function migrate() {
  // Skip until the first migration exists.
  if (!existsSync(MIGRATIONS_DIR)) return;
  const options = { cwd: BACKEND_DIR, stdio: "inherit", env: { ...process.env, EF_CONNECTION: E2E_DB } };
  // Build once without applying migrations to the dev database, then run EF without rebuilding.
  execFileSync("dotnet", ["build", "src/ContractReview.Api", "--property:ApplyMigrationsOnBuild=false", "--nologo"], options);
  execFileSync(
    "dotnet",
    [
      "tool",
      "run",
      "dotnet-ef",
      "database",
      "update",
      "--project",
      "src/ContractReview.Infrastructure",
      "--startup-project",
      "src/ContractReview.Api",
      "--no-build",
    ],
    options,
  );
}

if (process.argv[1] === fileURLToPath(import.meta.url)) migrate();
