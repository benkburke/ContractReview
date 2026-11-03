import { hubMethods } from "@/constants/realtime";

/** Mirrors the C# `ClaimProgressEvent` pushed by `IClaimProgressClient.EvaluationProgress`. */
export const claimProgressSteps = {
  retrieving: "Retrieving",
  reasoning: "Reasoning",
  reviewing: "Reviewing",
  completed: "Completed",
  failed: "Failed",
} as const;

export type ClaimProgressStep =
  (typeof claimProgressSteps)[keyof typeof claimProgressSteps];

export type ClaimProgressEvent = {
  contractId: string;
  step: ClaimProgressStep;
  occurredAtUtc: string;
  claimId: string | null;
  errorCode: string | null;
};

/** Mirrors the C# `IClaimProgressClient` (server-to-client methods). */
export type ClaimProgressClient = {
  [hubMethods.evaluationProgress]: (event: ClaimProgressEvent) => void;
};
