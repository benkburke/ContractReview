#!/usr/bin/env node
// Blocks shell commands that name a secret file (.env, .env.local, .env.<env>, .env.<env>.local,
// appsettings.<env>.local.json) in any form: cat, type, Get-Content, cp, node -e readFileSync(...), etc.
// `.env.example` stays allowed. Runs for the main session and every subagent (project-level hook).
let raw = "";
for await (const chunk of process.stdin) raw += chunk;

let cmd;
try {
  cmd = JSON.parse(raw)?.tool_input?.command ?? "";
} catch {
  console.error("guard-secrets: could not parse hook input; blocking to be safe.");
  process.exit(2);
}

const envFile = /(^|[\s"'`\/\\=(,:])\.env(\.(local|development|production|test)(\.local)?)?(?=$|[\s"'`;|&)<>,\\])/i;
const localAppSettings = /appsettings\.[\w-]+\.local\.json/i;

if (envFile.test(cmd) || localAppSettings.test(cmd)) {
  console.error("Blocked by guard-secrets: commands may not read, copy or print secret files (.env*, appsettings.*.local.json). Ask the human to handle secrets.");
  process.exit(2);
}
process.exit(0);
