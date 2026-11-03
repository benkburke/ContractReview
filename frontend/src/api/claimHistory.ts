import { apiClient } from "@/api/client";
import type { components } from "@/api/generated/schema";
import {
  mapClaimHistoryProblem,
  readProblem,
  type ProblemInfo,
} from "@/api/problem";
import { claimHistoryLimits } from "@/constants/claims";
import { contractErrorStrings } from "@/strings/contracts";

export type ClaimHistoryPage = components["schemas"]["ClaimHistoryPageDto"];
export type ClaimSummary = components["schemas"]["ClaimSummaryDto"];

export type HistoryOutcome =
  { ok: true; page: ClaimHistoryPage } | { ok: false; message: string };

/** Always fetched fresh (no-store) so a just-evaluated claim shows at the top on return. */
export async function fetchClaimHistory(
  contractId: string,
  page: number,
): Promise<HistoryOutcome> {
  try {
    const { data, error, response } = await apiClient.GET(
      "/api/contracts/{id}/claims",
      {
        params: {
          path: { id: contractId },
          query: { page, pageSize: claimHistoryLimits.pageSize },
        },
        cache: "no-store",
      },
    );
    if (data) return { ok: true, page: data };
    const problem: ProblemInfo = readProblem(error, response.status);
    return { ok: false, message: mapClaimHistoryProblem(problem) };
  } catch {
    return { ok: false, message: contractErrorStrings.network };
  }
}
