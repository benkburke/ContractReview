Problem: Azure SQL migration job fails during azure/login because no subscription ID input is resolved.
Root cause: AZURE_SUBSCRIPTION_ID is not resolved from the production environment, though the workflow requires it.
Change: Accept the ID from either the existing secret or environment variable; fail early with setup guidance if neither exists.
Owner: backend/deployment workflow.
Regression check: confirm the preflight and login use the same secret-or-variable fallback; validate workflow syntax and repository checks.
