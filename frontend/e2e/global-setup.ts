import { execFileSync } from "node:child_process";
import path from "node:path";

const MIGRATE_SCRIPT = path.join(__dirname, "migrate.mjs");

// The backend webServer command also migrates first (webServer starts before globalSetup); this is idempotent.
export default function globalSetup() {
  execFileSync(process.execPath, [MIGRATE_SCRIPT], { stdio: "inherit" });
}
