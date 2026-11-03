# Realtime

## Claim progress hub

Hub: `/hubs/claim-progress` (anonymous, no authentication). The hub has no client-to-server methods.

| Event | Payload | Notes |
|-------|---------|-------|
| `EvaluationProgress` | `{ contractId, step, occurredAtUtc, claimId, errorCode }` | `step` is `Retrieving`, `Reasoning`, `Reviewing`, `Completed` or `Failed`. `Reviewing` is sent between `Reasoning` and `Completed` only when a review runs. `claimId` is set on `Completed` and `Failed`. `errorCode` is set on `Failed`. |

### Opting in

Progress is opt-in per request:

1. The client opens a SignalR connection to `/hubs/claim-progress` and reads its connection id.
2. The client sends that id in the optional `X-SignalR-Connection-Id` header on `POST /api/contracts/{id}/claims`. Ids longer than 200 characters are ignored.
3. Only that connection receives the `EvaluationProgress` events for the evaluation.

Without the header, evaluation runs normally and no progress events are sent. Spec: `specs/live-evaluation-progress.md`.
