import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { components } from "@/api/generated/schema";
import { ClaimResultView } from "@/components/organisms/ClaimResultView";
import { testIds } from "@/constants/testIds";
import { claimStrings } from "@/strings/claims";

const get = vi.hoisted(() => vi.fn());
vi.mock("@/api/client", () => ({ apiClient: { GET: get } }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ push: vi.fn() }) }));

type Claim = components["schemas"]["ClaimEvaluationDto"];
type Result = components["schemas"]["ClaimEvaluationResultDto"];
type Review = components["schemas"]["ClaimReviewDto"];

const CONTRACT = "11111111-1111-4111-8111-111111111111";
const CLAIM = "22222222-2222-4222-8222-222222222222";
const text = claimStrings.review;

function review(over: Partial<Review> = {}): Review {
  return {
    state: "Completed",
    agreesWithPrimary: true,
    isCovered: true,
    confidence: 77,
    accuracyExplanation: "The clause matches.",
    coveredItems: ["radiator"],
    notCoveredItems: ["hoses"],
    model: "reviewer-model",
    errorCode: null,
    ...over,
  };
}

function claim(
  reviewValue: Review | null,
  result: Partial<Result> = {},
): Claim {
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
      citations: [],
      model: "fake",
      ...result,
    },
    review: reviewValue,
  } as Claim;
}

async function show(value: Claim) {
  get.mockResolvedValue({ data: value, response: { status: 200 } });
  render(<ClaimResultView contractId={CONTRACT} claimId={CLAIM} />);
  await screen.findByTestId(testIds.claimReviewPanel);
}

describe("ClaimResultView review panel", () => {
  beforeEach(() => get.mockReset());
  afterEach(cleanup);

  it("starts collapsed and shows an agreeing review without a banner", async () => {
    await show(claim(review()));
    const summary = screen.getByTestId(testIds.claimReviewSummary);
    expect(summary).toHaveAttribute("aria-expanded", "false");
    await userEvent.click(summary);
    expect(summary).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByTestId(testIds.claimReviewAgreement)).toHaveTextContent(text.agrees);
    expect(screen.getByRole("progressbar", { name: text.confidenceLabel })).toBeInTheDocument();
    expect(screen.getByTestId(testIds.claimReviewConfidence)).toHaveTextContent("77");
    expect(screen.getByTestId(testIds.claimReviewCovered)).toHaveTextContent(text.yes);
    expect(screen.getByTestId(testIds.claimReviewExplanation)).toHaveTextContent("The clause matches.");
    expect(screen.getByTestId(testIds.claimReviewCoveredItems)).toHaveTextContent("radiator");
    expect(screen.getByTestId(testIds.claimReviewNotCoveredItems)).toHaveTextContent("hoses");
    expect(screen.getByTestId(testIds.claimReviewModel)).toHaveTextContent("reviewer-model");
    expect(screen.queryByTestId(testIds.claimReviewDisagreeBanner)).not.toBeInTheDocument();
  });

  it("shows a banner when the review disagrees and the button expands the panel and focuses its summary", async () => {
    await show(claim(review({ agreesWithPrimary: false, isCovered: false })));
    const banner = screen.getByTestId(testIds.claimReviewDisagreeBanner);
    expect(banner).toHaveTextContent(text.disagreeBanner);
    expect(banner).not.toHaveAttribute("role", "alert");
    const summary = screen.getByTestId(testIds.claimReviewSummary);
    expect(summary).toHaveAttribute("aria-expanded", "false");
    await userEvent.click(screen.getByTestId(testIds.claimReviewDisagreeAction));
    expect(summary).toHaveAttribute("aria-expanded", "true");
    expect(summary).toHaveFocus();
    expect(screen.getByTestId(testIds.claimReviewAgreement)).toHaveTextContent(text.disagrees);
    expect(screen.getByTestId(testIds.claimReviewCovered)).toHaveTextContent(text.no);
  });

  it("says the review was unavailable when it failed, with no banner", async () => {
    await show(
      claim(
        review({ state: "Failed", agreesWithPrimary: null, isCovered: null, confidence: null, errorCode: "x" }),
      ),
    );
    await userEvent.click(screen.getByTestId(testIds.claimReviewSummary));
    expect(screen.getByTestId(testIds.claimReviewNote)).toHaveTextContent(text.unavailable);
    expect(screen.queryByTestId(testIds.claimReviewDisagreeBanner)).not.toBeInTheDocument();
  });

  it("says not reviewed for an older claim with no review", async () => {
    await show(claim(null));
    await userEvent.click(screen.getByTestId(testIds.claimReviewSummary));
    expect(screen.getByTestId(testIds.claimReviewNote)).toHaveTextContent(/^Not reviewed$/);
  });

  it("says the primary answer asked for more information when status is InsufficientInformation", async () => {
    await show(claim(null, { status: "InsufficientInformation" }));
    await userEvent.click(screen.getByTestId(testIds.claimReviewSummary));
    expect(screen.getByTestId(testIds.claimReviewNote)).toHaveTextContent(text.notReviewedMoreInfo);
  });
});
