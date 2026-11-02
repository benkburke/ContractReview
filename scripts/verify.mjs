#!/usr/bin/env node
// Single definition of done, with quiet output.
// Usage: node scripts/verify.mjs [--scope backend|frontend] [--fast] [--e2e]
//   --scope  run one side only (default: both, plus the API client drift check)
//   --fast   inner-loop check: skip the format check and the production frontend build
//   --e2e    add Playwright end-to-end tests (the full gate)
// Prints one line per step. On failure it prints only the most relevant lines and the log path.
// Every step's full output goes to .verify/<step>.log. A passing run is recorded in
// .verify/result.json with a fingerprint of the working tree, so an identical rerun returns at once.
import { spawnSync } from "node:child_process";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const argv = process.argv.slice(2);
const scopeAt = argv.indexOf("--scope");
const scope = scopeAt >= 0 ? argv[scopeAt + 1] : "all";
if (!["all", "backend", "frontend"].includes(scope)) {
  console.error("Usage: node scripts/verify.mjs [--scope backend|frontend] [--fast] [--e2e]");
  process.exit(64);
}
const mode = { scope, fast: argv.includes("--fast"), e2e: argv.includes("--e2e") };
const outDir = path.join(root, ".verify");
const resultFile = path.join(outDir, "result.json");
fs.mkdirSync(outDir, { recursive: true });

const git = (args) => {
  const r = spawnSync("git", args, { cwd: root, encoding: "utf8", maxBuffer: 64 * 1024 * 1024 });
  return r.status === 0 ? r.stdout : "";
};
// Fingerprint = HEAD plus the content of every file that differs from HEAD (tracked or untracked).
const fingerprint = () => {
  const h = crypto.createHash("sha256");
  h.update(git(["rev-parse", "HEAD"]));
  const changed = new Set(
    [...git(["diff", "HEAD", "--name-only"]).split("\n"), ...git(["ls-files", "--others", "--exclude-standard"]).split("\n")]
      .map((f) => f.trim())
      .filter((f) => f && !f.startsWith(".verify/")),
  );
  for (const f of [...changed].sort()) {
    h.update(`\0${f}\0`);
    try {
      h.update(fs.readFileSync(path.join(root, f)));
    } catch {
      h.update("<deleted>");
    }
  }
  return h.digest("hex");
};
// A passing run covers a new request when it was at least as broad.
const covers = (prev) =>
  prev.ok && (prev.mode.scope === "all" || prev.mode.scope === mode.scope) && (!prev.mode.fast || mode.fast) && (prev.mode.e2e || !mode.e2e);

try {
  const prev = JSON.parse(fs.readFileSync(resultFile, "utf8"));
  if (prev.fingerprint === fingerprint() && covers(prev)) {
    console.log(`VERIFY: OK (cached: nothing changed since the passing run at ${prev.at})`);
    process.exit(0);
  }
} catch {}

const steps = [];
if (scope !== "frontend") {
  steps.push(["backend", "dotnet tool restore"]);
  steps.push(["backend", "dotnet build -warnaserror -nologo -p:ApplyMigrationsOnBuild=false"]);
  if (!mode.fast) steps.push(["backend", "dotnet format --verify-no-changes --no-restore"]);
  steps.push(["backend", "dotnet test --no-build --nologo"]);
}
if (scope !== "backend") {
  if (scope === "all") {
    steps.push(["frontend", "npm run gen:api --silent"]);
    steps.push(["frontend", "git diff --exit-code --stat -- src/api/generated"]);
  }
  steps.push(["frontend", "npm run lint --silent"]);
  steps.push(["frontend", "npm run typecheck --silent"]);
  steps.push(["frontend", "npm run test --silent"]);
  if (!mode.fast) steps.push(["frontend", "npm run build --silent"]);
}
if (mode.e2e) steps.push(["frontend", "npx playwright test --reporter=line --pass-with-no-tests"]);

const env = { ...process.env, NO_COLOR: "1", FORCE_COLOR: "0", NEXT_TELEMETRY_DISABLED: "1", DOTNET_CLI_TELEMETRY_OPTOUT: "1", DOTNET_NOLOGO: "1" };
const slug = (s) => s.replace(/[^a-z0-9]+/gi, "-").replace(/^-|-$/g, "").toLowerCase().slice(0, 60);
const relevant = (out) => {
  const lines = out.split(/\r?\n/).map((l) => l.trimEnd()).filter(Boolean);
  const hits = lines.filter((l) => /\b(error|errors|failed|failure|exception|expected|received|assert)\b|\bFAIL\b|✘|×/i.test(l));
  return [...new Set(hits.length ? hits : lines.slice(-40))].slice(0, 40);
};

const started = Date.now();
for (const [dir, cmd] of steps) {
  const t = Date.now();
  const r = spawnSync(cmd, { cwd: path.join(root, dir), encoding: "utf8", shell: true, env, maxBuffer: 256 * 1024 * 1024 });
  const out = `${r.stdout ?? ""}${r.stderr ?? ""}${r.error ? `\n${r.error}` : ""}`;
  const logName = `${slug(`${dir}-${cmd}`)}.log`;
  fs.writeFileSync(path.join(outDir, logName), out);
  const secs = ((Date.now() - t) / 1000).toFixed(1);
  if (r.status === 0) {
    console.log(`PASS [${dir}] ${cmd} (${secs}s)`);
    // Lets Playwright reuse this production build instead of building the frontend again.
    if (cmd.startsWith("npm run build")) env.VERIFY_BUILT = "1";
    continue;
  }
  console.log(`FAIL [${dir}] ${cmd} (${secs}s)`);
  console.log(relevant(out).map((l) => `  ${l.slice(0, 300)}`).join("\n"));
  console.log(`Full log: .verify/${logName}`);
  fs.writeFileSync(resultFile, JSON.stringify({ ok: false, mode, failedStep: `[${dir}] ${cmd}`, log: `.verify/${logName}`, at: new Date().toISOString() }, null, 2));
  process.exit(r.status || 1);
}
fs.writeFileSync(resultFile, JSON.stringify({ ok: true, mode, fingerprint: fingerprint(), at: new Date().toISOString() }, null, 2));
console.log(`VERIFY: OK (${steps.length} steps, ${Math.round((Date.now() - started) / 1000)}s)`);
