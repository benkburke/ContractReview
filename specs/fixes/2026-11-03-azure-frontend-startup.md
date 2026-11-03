Problem: Azure Portal opens a nonexistent slot; the actual frontend returns HTTP 503.
Evidence: No deployment slots exist; the hostname suffix is dkhybzcugzg7dzar.
Root cause: Startup command was empty although deployment uses Next.js standalone output.
Repair: Set the existing frontend App Service startup command to node server.js.
Owner: Coordinator performing Azure configuration repair; no application code changes.
Verification: Saved startup command confirmed; root redirects to /contracts, which returns HTTP 200.
Boundary: No auth, data, runtime-version, or deployment-slot changes.
Remaining: Deployment 41567150207 built with the corrected API origin, but live HTML still references old chunks.
Follow-up: API /api/contracts now returns HTTP 200; latest HTML on disk differs from served HTML even after restart.
Approved follow-up: Human approved clean frontend deployment; added clean/restart inputs; live verification pending.
