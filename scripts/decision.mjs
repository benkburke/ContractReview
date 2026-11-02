#!/usr/bin/env node
// Appends one entry to DECISIONS.md with the next ID, so the coordinator never reads or rewrites the log.
// Usage: node scripts/decision.mjs --question "..." --choice "..." --why "..." --by <who>
//          [--options "A | B | C"] [--source specs/x.md] [--reverses D-003]
// <who>: human | claude | human-override | claude-challenge
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const file = path.join(root, "DECISIONS.md");
const WHO = {
  human: "You chose",
  claude: "Claude proposed; you accepted",
  "human-override": "You overrode Claude's proposal",
  "claude-challenge": "Claude pushed back; you decided",
};
const REQUIRED = ["question", "choice", "why", "by"];
const HEADER = "# Decisions\n\nRecorded by the coordinator with `node scripts/decision.mjs`. Append-only: a changed decision gets a new entry that reverses the old one.\n";

const args = {};
const argv = process.argv.slice(2);
for (let i = 0; i < argv.length; i++) {
  if (argv[i].startsWith("--")) {
    args[argv[i].slice(2)] = argv[i + 1] ?? "";
    i++;
  }
}
const missing = REQUIRED.filter((k) => !args[k]?.trim());
if (missing.length || !WHO[args.by]) {
  console.error(`decision.mjs: missing ${missing.join(", ") || "nothing"}; --by must be one of ${Object.keys(WHO).join(", ")}`);
  process.exit(64);
}
const oneLine = (s) => s.replace(/\s+/g, " ").trim();

let text = fs.existsSync(file) ? fs.readFileSync(file, "utf8") : HEADER;
const ids = [...text.matchAll(/^## D-(\d{3})/gm)].map((m) => Number(m[1]));
const id = `D-${String((ids.length ? Math.max(...ids) : 0) + 1).padStart(3, "0")}`;

if (args.reverses) {
  const heading = new RegExp(`^(## ${args.reverses.replace(/[^\w-]/g, "")}\\b.*)$`, "m");
  if (!heading.test(text)) {
    console.error(`decision.mjs: no entry ${args.reverses} to reverse`);
    process.exit(65);
  }
  text = text.replace(heading, `$1 (reversed by ${id})`);
}

// Local date (YYYY-MM-DD), so an evening decision is not dated tomorrow.
const lines = [`## ${id} ${oneLine(args.question)}`, `- Date: ${new Date().toLocaleDateString("en-CA")}`];
if (args.options) lines.push(`- Options: ${oneLine(args.options)}`);
lines.push(`- Choice: ${oneLine(args.choice)}`, `- Why: ${oneLine(args.why)}`, `- Who: ${WHO[args.by]}`);
if (args.reverses) lines.push(`- Reverses: ${args.reverses}`);
if (args.source) lines.push(`- Source: ${oneLine(args.source)}`);

fs.writeFileSync(file, `${text.replace(/\s*$/, "")}\n\n${lines.join("\n")}\n`);
console.log(`Recorded ${id}`);
