import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { components } from "@/api/generated/schema";
import { AskClaimForm } from "@/components/organisms/AskClaimForm";
import { ClaimResultView } from "@/components/organisms/ClaimResultView";
import { ContractDetailView } from "@/components/organisms/ContractDetailView";
import { testIds } from "@/constants/testIds";
import { claimStrings } from "@/strings/claims";
import { indexStrings } from "@/strings/contracts";

const post = vi.hoisted(() => vi.fn());
const get = vi.hoisted(() => vi.fn());
const push = vi.hoisted(() => vi.fn());
vi.mock("@/api/client", () => ({ apiClient: { POST: post, GET: get } }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));

type Claim = components["schemas"]["ClaimEvaluationDto"];
type Detail = components["schemas"]["ContractDetailDto"];

const CONTRACT = "11111111-1111-4111-8111-111111111111";
const CLAIM = "22222222-2222-4222-8222-222222222222";

function insufficientClaim(): Claim {
  return {
    id: CLAIM,
    contractId: CONTRACT,
    claimText: "My radiator is leaking",
    coverageLevelRequested: null,
    state: "Evaluated",
    createdAtUtc: "2026-01-01T00:00:00Z",
    errorCode: null,
    result: {
      status: "InsufficientInformation",
      confidence: 30,
      coverageLevel: null,
      needsCoverageLevel: false,
      coverageLevelOptions: [],
      coveredItems: [],
      excludedItems: [],
      reasoning: "Not enough detail.",
      followUpQuestion: null,
      citations: [],
      model: "fake",
    },
  } as Claim;
}

function indexedContract(): Detail {
  return {
    id: CONTRACT,
    title: "Warranty",
    fileName: "w.pdf",
    sizeBytes: 1000,
    pageCount: 2,
    uploadedAtUtc: "2026-01-01T00:00:00Z",
    extractedText: "Full contract text",
    indexStatus: "Indexed",
    sections: [{ order: 1, heading: "COVERAGE SCHEDULE", chunkCount: 2 }],
  } as Detail;
}

describe("claim form stuck and follow-up regressions", () => {
  beforeEach(() => {
    post.mockReset();
    get.mockReset();
    push.mockReset();
  });
  afterEach(cleanup);

  it("AskClaimForm resets busy after a successful submit navigation", async () => {
    post.mockResolvedValue({ data: insufficientClaim(), response: { status: 201 } });
    render(<AskClaimForm contractId={CONTRACT} />);
    await userEvent.type(screen.getByTestId(testIds.claimTextInput), "Is the radiator covered?");
    await userEvent.click(screen.getByTestId(testIds.claimSubmit));
    await waitFor(() => expect(push).toHaveBeenCalled());
    await waitFor(() => {
      const button = screen.getByTestId(testIds.claimSubmit);
      expect(button).toBeEnabled();
      expect(button).toHaveTextContent(claimStrings.evaluate);
      expect(button).not.toHaveTextContent(claimStrings.evaluating);
    });
  });

  it("ClaimResultView shows the follow-up prompt for InsufficientInformation with null question", async () => {
    get.mockResolvedValue({ data: insufficientClaim(), response: { status: 200 } });
    render(<ClaimResultView contractId={CONTRACT} claimId={CLAIM} />);
    await screen.findByTestId(testIds.claimStatusBanner);
    expect(screen.getByTestId(testIds.claimFollowUp)).toBeInTheDocument();
  });

  it("ContractDetailView renders the claim form before the sections heading and extracted text", async () => {
    get.mockResolvedValue({ data: indexedContract(), response: { status: 200 } });
    render(<ContractDetailView id={CONTRACT} />);
    const form = await screen.findByTestId(testIds.claimForm);
    const heading = screen.getByRole("heading", { name: indexStrings.sectionsHeading });
    const text = screen.getByTestId(testIds.contractDetailText);
    expect(form.compareDocumentPosition(heading) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(form.compareDocumentPosition(text) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });
});
