import { apiClient } from "@/api/client";
import { mapClaimProblem, readProblem, type ProblemInfo } from "@/api/problem";
import { contractErrorStrings } from "@/strings/contracts";

export type EvaluateOutcome =
  | { ok: true; claimId: string }
  | { ok: false; field: "text" | "coverage" | "form"; message: string };

/** Single place that posts a claim; used by the ask form and "Evaluate again". */
export async function evaluateClaim(
  contractId: string,
  claimText: string,
  coverageLevel: string | null,
): Promise<EvaluateOutcome> {
  try {
    const { data, error, response } = await apiClient.POST(
      "/api/contracts/{id}/claims",
      {
        params: { path: { id: contractId } },
        body: { claimText, coverageLevel },
      },
    );
    if (data) return { ok: true, claimId: data.id };
    const problem: ProblemInfo = readProblem(error, response.status);
    return { ok: false, ...mapClaimProblem(problem) };
  } catch {
    return { ok: false, field: "form", message: contractErrorStrings.network };
  }
}
