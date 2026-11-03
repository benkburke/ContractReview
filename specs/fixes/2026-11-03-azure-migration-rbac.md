Problem: Production deployment stops at Azure login before EF Core migrations run.
Root cause: contractreview-identity has no RBAC assignments on the SQL server or inherited scopes.
Evidence: Azure CLI lists no effective assignments; the latest deployment reports no subscriptions found.
Federation: Existing issuer, audience and subject match the failed job's OIDC claims.
Repair: Granted SQL Server Contributor to the existing identity on contractreview-db only.
Approval: Human approved SQL-server-scoped SQL Server Contributor; no subscription-wide role granted.
Owner: Coordinator performing the approved Azure configuration repair; no application code changes.
Verification: Deployment 41561998655 attempt 2 passed login, firewall setup/cleanup, migrations and both app deployments.
Production approval: Human approved migrations and deployment, then started the rerun; all three migrations completed.
