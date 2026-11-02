import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { components } from "@/api/generated/schema";
import { AskClaimForm } from "@/components/organisms/AskClaimForm";
import { ClaimResultView } from "@/components/organisms/ClaimResultView";
import { errorCodes } from "@/constants/errorCodes";
import { routes } from "@/constants/routes";
import { testIds } from "@/constants/testIds";
import { claimErrorStrings, claimStrings } from "@/strings/claims";

const post = vi.hoisted(() => vi.fn());
const get = vi.hoisted(() => vi.fn());
const push = vi.hoisted(() => vi.fn());
vi.mock("@/api/client", () => ({ apiClient: { POST: post, GET: get } }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));

type Claim = components["schemas"]["ClaimEvaluationDto"];
type Result = components["schemas"]["ClaimEvaluationResultDto"];

const CONTRACT = "11111111-1111-4111-8111-111111111111";
const CLAIM = "22222222-2222-4222-8222-222222222222";
const NEW_CLAIM = "33333333-3333-4333-8333-333333333333";

function claim(result: Partial<Result> = {}, over: Partial<Claim> = {}): Claim {
  return {
    id: CLAIM,
    contractId: CONTRACT,
    claimText: "My radiator is leaking",
    coverageLevelRequested: null,
    state: "Evaluated",
    createdAtUtc: "2026-01-01T00:00:00Z",
    errorCode: null,
    result: {
      status: "Covered",
      confidence: 85,
      coverageLevel: "Gold",
      needsCoverageLevel: false,
      coverageLevelOptions: [],
      coveredItems: ["radiator"],
      excludedItems: [],
      reasoning: "The contract lists it.",
      followUpQuestion: null,
      citations: [{ sectionHeading: "COVERAGE SCHEDULE", excerpt: "The radiator is covered." }],
      model: "fake",
      ...result,
    },
    ...over,
  } as Claim;
}

describe("AskClaimForm", () => {
  beforeEach(() => {
    post.mockReset();
    push.mockReset();
  });
  afterEach(cleanup);

  it("posts the trimmed claim and opens the dashboard on 201", async () => {
    post.mockResolvedValue({ data: claim(), response: { status: 201 } });
    render(<AskClaimForm contractId={CONTRACT} />);
    await userEvent.type(screen.getByTestId(testIds.claimTextInput), "  Is the radiator covered?  ");
    await userEvent.click(screen.getByTestId(testIds.claimSubmit));
    await waitFor(() => expect(push).toHaveBeenCalledWith(routes.claimDetail(CONTRACT, CLAIM)));
    expect(post.mock.calls[0][1].body).toEqual({ claimText: "Is the radiator covered?", coverageLevel: null });
  });

  it("shows a field error and does not post when the text is too short", async () => {
    render(<AskClaimForm contractId={CONTRACT} />);
    await userEvent.type(screen.getByTestId(testIds.claimTextInput), "abc");
    await userEvent.click(screen.getByTestId(testIds.claimSubmit));
    expect(await screen.findByTestId(testIds.claimTextError)).toHaveTextContent(claimErrorStrings.textInvalid);
    expect(post).not.toHaveBeenCalled();
  });

  it("shows the evaluation error when the API answers 502", async () => {
    post.mockResolvedValue({
      error: { code: errorCodes.claimEvaluationFailed, title: "x", status: 502 },
      response: { status: 502 },
    });
    render(<AskClaimForm contractId={CONTRACT} />);
    await userEvent.type(screen.getByTestId(testIds.claimTextInput), "Is the radiator covered?");
    await userEvent.click(screen.getByTestId(testIds.claimSubmit));
    expect(await screen.findByTestId(testIds.claimFormError)).toHaveTextContent(claimErrorStrings.evaluationFailed);
    expect(push).not.toHaveBeenCalled();
    expect(screen.getByTestId(testIds.claimSubmit)).toBeEnabled();
  });
});

describe("ClaimResultView", () => {
  beforeEach(() => {
    post.mockReset();
    get.mockReset();
    push.mockReset();
  });
  afterEach(cleanup);

  it("shows status, confidence, level, reasoning, items and cited clauses", async () => {
    get.mockResolvedValue({ data: claim(), response: { status: 200 } });
    render(<ClaimResultView contractId={CONTRACT} claimId={CLAIM} />);
    expect(await screen.findByTestId(testIds.claimStatusBanner)).toHaveTextContent(claimStrings.status.Covered);
    expect(screen.getByTestId(testIds.claimConfidence)).toHaveTextContent("85");
    expect(screen.getByTestId(testIds.claimCoverageLevel)).toHaveTextContent("Gold");
    expect(screen.getByTestId(testIds.claimReasoning)).toHaveTextContent("The contract lists it.");
    expect(screen.getByTestId(testIds.claimCoveredItems)).toHaveTextContent("radiator");
    expect(screen.getByTestId(testIds.claimExcludedItems)).toBeInTheDocument();
    const cite = screen.getByTestId(testIds.claimCitationItem);
    expect(cite).toHaveTextContent("COVERAGE SCHEDULE");
    expect(cite).toHaveTextContent("The radiator is covered.");
    expect(screen.queryByTestId(testIds.claimLevelPicker)).not.toBeInTheDocument();
  });

  it("shows the follow-up question for insufficient information", async () => {
    get.mockResolvedValue({
      data: claim({ status: "InsufficientInformation", followUpQuestion: "Describe the symptoms.", citations: [] }),
      response: { status: 200 },
    });
    render(<ClaimResultView contractId={CONTRACT} claimId={CLAIM} />);
    expect(await screen.findByTestId(testIds.claimFollowUp)).toHaveTextContent("Describe the symptoms.");
    expect(screen.getByTestId(testIds.claimStatusBanner)).toHaveTextContent(claimStrings.status.InsufficientInformation);
  });

  it("offers the level picker and Evaluate again submits the same text with the chosen level", async () => {
    get.mockResolvedValue({
      data: claim({
        status: "InsufficientInformation",
        needsCoverageLevel: true,
        coverageLevelOptions: ["Powertrain", "Gold"],
        coverageLevel: null,
        citations: [],
      }),
      response: { status: 200 },
    });
    post.mockResolvedValue({ data: claim({}, { id: NEW_CLAIM }), response: { status: 201 } });
    render(<ClaimResultView contractId={CONTRACT} claimId={CLAIM} />);
    const picker = await screen.findByTestId(testIds.claimLevelPicker);
    const button = screen.getByTestId(testIds.claimReevaluate);
    expect(button).toBeDisabled();
    await userEvent.click(within(picker).getByLabelText("Gold"));
    await userEvent.click(button);
    await waitFor(() => expect(push).toHaveBeenCalledWith(routes.claimDetail(CONTRACT, NEW_CLAIM)));
    expect(post.mock.calls[0][1].body).toEqual({ claimText: "My radiator is leaking", coverageLevel: "Gold" });
  });

  it("shows the failed message for a Failed claim", async () => {
    get.mockResolvedValue({
      data: { ...claim(), state: "Failed", result: null, errorCode: errorCodes.claimEvaluationFailed },
      response: { status: 200 },
    });
    render(<ClaimResultView contractId={CONTRACT} claimId={CLAIM} />);
    expect(await screen.findByTestId(testIds.claimFailed)).toHaveTextContent(claimStrings.failedHeading);
    expect(screen.queryByTestId(testIds.claimStatusBanner)).not.toBeInTheDocument();
  });

  it("shows not found for an unknown claim", async () => {
    get.mockResolvedValue({
      error: { code: errorCodes.claimNotFound, title: "x", status: 404 },
      response: { status: 404 },
    });
    render(<ClaimResultView contractId={CONTRACT} claimId={CLAIM} />);
    expect(await screen.findByTestId(testIds.claimNotFound)).toBeInTheDocument();
  });
});
