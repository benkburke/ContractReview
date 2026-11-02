import { defineConfig } from "@playwright/test";

const API_URL = process.env.E2E_API_URL ?? "http://localhost:5080";
const WEB_URL = process.env.E2E_WEB_URL ?? "http://localhost:3000";
const E2E_DB =
  "Server=(localdb)\\MSSQLLocalDB;Database=ContractReview_E2E;Trusted_Connection=True;TrustServerCertificate=True";

const webCommand = process.env.VERIFY_BUILT
  ? "npm run start"
  : process.env.CI
    ? "npm run build && npm run start"
    : "npm run dev";

export default defineConfig({
  testDir: "e2e",
  globalSetup: "e2e/global-setup.ts",
  reporter: "line",
  use: { baseURL: WEB_URL },
  webServer: [
    {
      command: `node e2e/migrate.mjs && dotnet run --project ../backend/src/ContractReview.Api --urls ${API_URL} --property:ApplyMigrationsOnBuild=false`,
      url: `${API_URL}/health`,
      env: {
        ASPNETCORE_ENVIRONMENT: "Development",
        ConnectionStrings__AppDb: E2E_DB,
        Embeddings__Provider: "Fake",
        Llm__Provider: "Fake",
      },
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
    },
    {
      command: webCommand,
      url: WEB_URL,
      reuseExistingServer: !process.env.CI,
      timeout: 180_000,
    },
  ],
});
