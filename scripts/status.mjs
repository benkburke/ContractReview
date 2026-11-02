#!/usr/bin/env node
// SessionStart hook: prints a short project status into the session's context, so the coordinator
// does not spend a model turn (or a subagent) re-checking the toolchain and project state.
// Read-only, quick, and never fails the session.
import { execSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";

const root = process.env.CLAUDE_PROJECT_DIR || process.cwd();
const sh = (cmd) => {
  try {
    return execSync(cmd, { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "ignore"], timeout: 15000 }).trim();
  } catch {
    return "";
  }
};
const has = (p) => fs.existsSync(path.join(root, p));
const firstLine = (s) => s.split(/\r?\n/)[0] || "";

try {
  const ver = (cmd, strip = "") => firstLine(sh(cmd)).replace(strip, "") || "MISSING";
  const localDb = /MSSQLLocalDB/i.test(sh("sqllocaldb info")) ? "ok" : "MISSING";
  const backendReady = has("backend") && fs.readdirSync(path.join(root, "backend")).some((f) => /\.slnx?$/i.test(f));
  const frontendReady = has("frontend/package.json");
  const specs = has("specs")
    ? fs.readdirSync(path.join(root, "specs"))
        .filter((f) => f.endsWith(".md") && !["roadmap.md", "challenge.md"].includes(f))
        .map((f) => {
          const head = fs.readFileSync(path.join(root, "specs", f), "utf8").slice(0, 800);
          const m = head.match(/^Status:\s*(.+)$/im);
          return { f, status: m ? m[1].trim() : "no Status line" };
        })
        .filter((s) => !/^done/i.test(s.status))
    : [];
  let verify = "never run";
  try {
    const v = JSON.parse(fs.readFileSync(path.join(root, ".verify", "result.json"), "utf8"));
    verify = v.ok ? `PASS ${v.mode?.scope ?? ""} ${v.mode?.e2e ? "with e2e " : ""}(${v.at})` : `FAIL at ${v.failedStep} (${v.at}), log ${v.log}`;
  } catch {}
  const dirty = sh("git status --porcelain").split(/\r?\n/).filter(Boolean).length;
  // Roadmap progress: "- [ ] T1 name: scope" / "- [x] D2 ..." checklist items in specs/roadmap.md.
  const roadmapText = has("specs/roadmap.md") ? fs.readFileSync(path.join(root, "specs", "roadmap.md"), "utf8") : "";
  const items = [...roadmapText.matchAll(/^\s*- \[( |x|X)\] ([TD]\d+)\b[^\n]*/gm)].map((m) => ({ done: m[1] !== " ", id: m[2], line: m[0].replace(/^\s*- \[.\] /, "").trim() }));
  const thin = items.filter((i) => i.id.startsWith("T"));
  const nextItem = items.find((i) => !i.done && (i.id.startsWith("T") || thin.every((t) => t.done)));
  const roadmap = !roadmapText
    ? "MISSING"
    : `thin path ${thin.filter((i) => i.done).length}/${thin.length} done${thin.length && thin.every((t) => t.done) ? " (complete; depth items next)" : ""}; next: ${nextItem ? nextItem.line.slice(0, 120) : "none"}`;
  let decisions = 0;
  try {
    decisions = (fs.readFileSync(path.join(root, "DECISIONS.md"), "utf8").match(/^## D-\d{3}/gm) || []).length;
  } catch {}
  const next = !has("docs/project.md")
    ? "run the project interview"
    : !(backendReady && frontendReady)
      ? "propose the bootstrap"
      : !roadmapText
        ? "propose the roadmap (thin path first)"
        : nextItem
          ? `suggest /feature ${nextItem.id}`
          : "roadmap done: ask what to work on next";
  console.log(
    [
      "## Session status (from scripts/status.mjs; do not re-run these checks)",
      `- Tools: git ${ver("git --version", "git version ")} | node ${ver("node --version")} | dotnet ${ver("dotnet --version")} | LocalDB ${localDb}`,
      `- Project: docs/project.md ${has("docs/project.md") ? "present" : "MISSING"} | backend ${backendReady ? "bootstrapped" : "not bootstrapped"} | frontend ${frontendReady ? "bootstrapped" : "not bootstrapped"}`,
      `- Git: ${firstLine(sh("git rev-parse --abbrev-ref HEAD")) || "no commits yet"} @ ${firstLine(sh("git log -1 --oneline")) || "none"} | ${dirty} uncommitted file(s)`,
      `- Last verify: ${verify}`,
      `- Open specs: ${specs.length ? specs.slice(0, 8).map((s) => `${s.f} (${s.status})`).join("; ") : "none"}`,
      `- Roadmap: ${roadmap}`,
      `- Decisions recorded: ${decisions} (DECISIONS.md)`,
      `- Next: ${next}`,
    ].join("\n"),
  );
} catch (e) {
  console.log(`## Session status unavailable (${e.message}); check the toolchain manually.`);
}
process.exit(0);
