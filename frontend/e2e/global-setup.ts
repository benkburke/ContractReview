import { execFileSync } from "node:child_process";
import { existsSync } from "node:fs";
import path from "node:path";

const E2E_DB =
  "Server=(localdb)\\MSSQLLocalDB;Database=ContractReview_E2E;Trusted_Connection=True;TrustServerCertificate=True";
const BACKEND_DIR = path.resolve(__dirname, "../../backend");
const MIGRATIONS_DIR = path.join(BACKEND_DIR, "src/ContractReview.Infrastructure/Migrations");

export default function globalSetup() {
  // Skip until the first migration exists.
  if (!existsSync(MIGRATIONS_DIR)) return;
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
    ],
    { cwd: BACKEND_DIR, stdio: "inherit", env: { ...process.env, EF_CONNECTION: E2E_DB } },
  );
}
