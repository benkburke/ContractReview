# Live evaluation progress
Status: done 2026-11-02
Roadmap: T4 (thin)

## Context
Last thin-path step. Evaluating a claim takes up to a minute (embed, retrieve, chat). Per D-019 the asking user sees live progress over SignalR. No auth (D-003), so "the asking user" is the browser's SignalR connection. Design per D-040: `POST /api/contracts/{id}/claims` stays synchronous; the frontend connects to the hub first and sends its connection id in a header; the handler pushes each step to that connection only. If the hub is not connected or the header is missing, evaluation still works without progress.

## Sides involved
Both: backend (hub, progress notifier, header handling, CORS) and frontend (hub connection, progress display in the Ask a claim form).

## Acceptance criteria
1. Given the claim form is open and the hub is connected, when the user submits a claim, then the form shows the steps in order: **Retrieving relevant clauses** → **Evaluating against the contract** → **Done** (then navigates to the result as today).
2. Given evaluation fails (502 `claim.evaluation_failed`), then a **Failed** step is pushed before the response, and the form shows the error as today.
3. Given two browsers evaluating claims at once, then each sees only its own steps.
4. Given the hub cannot connect (or drops), then submitting a claim still works with today's busy indicator; no error is shown for the missing progress.
5. Given an `X-SignalR-Connection-Id` header that is not a connected id, then the claim is evaluated normally (pushes to an unknown id are no-ops).
6. Validation errors (400/404/409) push no steps.

## Hub
- `ClaimProgressHub` at `/hubs/claim-progress` (route constant under `ApiRoutes.HubsPrefix`). No client-to-server methods.
- Client interface `IClaimProgressClient`, mirrored in `frontend/src/realtime/contracts.ts`:
  - `EvaluationProgress(ClaimProgressEvent e)`, where `ClaimProgressEvent` = `{ contractId: guid, step: "Retrieving" | "Reasoning" | "Completed" | "Failed", occurredAtUtc: datetime, claimId: guid | null, errorCode: string | null }`. `claimId` is set on Completed/Failed.
- Header name `X-SignalR-Connection-Id` (constant on both sides). Optional; max 200 chars, otherwise ignored.

## Endpoints
No new endpoints. `POST /api/contracts/{id}/claims` accepts the optional header above; request/response unchanged.

## Auth
None (D-003). The hub is anonymous. A connection id is an unguessable server-issued value, so pushing only to it keeps progress private enough for the no-auth model.

## Domain model
No domain change. Application: `IClaimProgressNotifier.NotifyAsync(connectionId?, ClaimProgressEvent)`; `EvaluateClaimCommand` gains an optional `ProgressConnectionId`. The handler calls it before retrieval, before the chat call, and after completion or failure. Notifier errors are logged and swallowed (never fail an evaluation). Api/Infrastructure: `SignalRClaimProgressNotifier` using `IHubContext<ClaimProgressHub, IClaimProgressClient>`.

## Data model changes
None.

## Deployment notes
- CORS policy for the frontend origin adds `AllowCredentials()` (SignalR negotiate needs it).
- Azure App Service: enable Web sockets on the API app (the client falls back to long polling otherwise). With more than one instance, ARR affinity must stay on; Azure SignalR Service is out of scope.

## Frontend
- `src/realtime/claimProgress.ts`: builds a `HubConnection` to `config.apiUrl + hub route` with automatic reconnect; started when the claim form mounts and stopped on unmount.
- The form passes the connection id header through the generated client when connected (via a custom header option, not a hand-written fetch).
- A labelled step list (MUI `Stepper`, vertical) under the Evaluate button while busy; the current step is marked with text, not colour alone.
- Strings in `src/strings/`, hub method and header names in `src/constants/`.

## Accessibility
The step list sits in a `role="status"` / `aria-live="polite"` region that announces each new step once; the Evaluate button keeps `aria-busy`; axe clean.

## Out of scope (Depth)
Async/background evaluation with 202 + reopen while running; Azure SignalR Service; dropped-connection failure tests (D7); a "Reviewing" step (added by D1).

## Open questions
None. Synchronous POST with per-connection push (D-040).
