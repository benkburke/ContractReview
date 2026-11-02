#!/usr/bin/env node
// Usage: node guard-path.mjs [--skip-subagents] <allowed-path> [<allowed-path>...]  (paths relative to repo root)
// Blocks Edit/Write outside the allowed paths. Works with Windows (C:\...), Git Bash (/c/...) and POSIX paths.
// --skip-subagents: for the main-session agent's guard. Its frontmatter hooks also fire for subagent tool
// calls, so without this flag a subagent would have to pass both its own guard and the coordinator's, and no
// path passes both. Claude Code puts `agent_id` in the hook input only for calls made inside a subagent;
// those calls are left to the subagent's own guard.
import path from "node:path";

const args = process.argv.slice(2);
const skipSubagents = args.includes("--skip-subagents");
const allowed = args.filter((a) => a !== "--skip-subagents");
let raw = "";
for await (const chunk of process.stdin) raw += chunk;

let input;
try {
  input = JSON.parse(raw);
} catch {
  console.error("guard-path: could not parse hook input; blocking to be safe.");
  process.exit(2);
}
if (skipSubagents && input?.agent_id) process.exit(0);
const file = input?.tool_input?.file_path;
if (!file) process.exit(0);

const isWin = process.platform === "win32";
// Convert Git Bash style /c/Users/... to C:/Users/... so Node resolves it correctly on Windows.
const fromMsys = (p) => (isWin ? p.replace(/^\/([a-zA-Z])\//, (_, d) => `${d.toUpperCase()}:/`) : p);
const norm = (p) => {
  const s = p.replace(/\\/g, "/").replace(/\/+$/, "");
  return isWin ? s.toLowerCase() : s;
};

const root = path.resolve(fromMsys(process.env.CLAUDE_PROJECT_DIR || process.cwd()));
const abs = path.resolve(root, fromMsys(file));
const rel = norm(path.relative(root, abs));

const inside = rel !== "" && !rel.startsWith("..") && !path.isAbsolute(rel);
const ok = inside && allowed.some((a) => {
  const A = norm(a);
  return rel === A || rel.startsWith(A + "/");
});

if (ok) process.exit(0);
console.error(`Blocked by guard-path: this agent may only edit: ${allowed.join(", ")} (attempted: ${rel || file})`);
process.exit(2);
