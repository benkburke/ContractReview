export const hubRoutes = {
  claimProgress: "/hubs/claim-progress",
} as const;

export const hubMethods = {
  evaluationProgress: "EvaluationProgress",
} as const;

export const hubHeaders = {
  connectionId: "X-SignalR-Connection-Id",
} as const;
