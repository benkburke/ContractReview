import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { components } from "@/api/generated/schema";
import * as claimsApi from "@/api/claims";
import { ClaimResultView } from "@/components/organisms/ClaimResultView";
import { testIds } from "@/constants/testIds";

const post = vi.hoisted(() => vi.fn());
const get = vi.hoisted(() => vi.fn());
const push = vi.hoisted(() => vi.fn());
vi.mock("@/api/client", () => ({ apiClient: { POST: post, GET: get } }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));

type Claim = components["schemas"]["ClaimEvaluationDto"];

const CONTRACT = "11111111-1111-4111-8111-111111111111";
const CLAIM = "22222222-2222-4222-8222-222222222222";
const NEW_CLAIM = "33333333-3333-4333-8333-333333333333";
const CLAIM_TEXT = "My radiator is leaking";
const QUESTION = "What specific coverage level do you have?";
const ANSWER = "Premium plan, bought in 2024";
const MAX = 2000;

function followUpClaim(): Claim {
  return {
    id: CLAIM,
    contractId: CONTRACT,
    claimText: CLAIM_TEXT,
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
      followUpQuestion: QUESTION,
      citations: [],
      model: "fake",
    },
  } as Claim;
}

describe("claim follow-up answer", () => {
  beforeEach(() => {
    post.mockReset();
    get.mockReset();
    push.mockReset();
    get.mockResolvedValue({ data: followUpClaim(), response: { status: 200 } });
  });
  afterEach(cleanup);

  it("renders an answer field when the follow-up has no level options", async () => {
    render(<ClaimResultView contractId={CONTRACT} claimId={CLAIM} />);
    await screen.findByTestId(testIds.claimFollowUp);
    expect(screen.getByRole("textbox", { name: /answer/i })).toBeInTheDocument();
    expect(screen.getByTestId(testIds.claimFollowUpSubmit)).toBeInTheDocument();
  });

  it("posts original text, question and answer then navigates to the new claim", async () => {
    post.mockResolvedValue({ data: { id: NEW_CLAIM }, response: { status: 201 } });
    render(<ClaimResultView contractId={CONTRACT} claimId={CLAIM} />);
    await screen.findByTestId(testIds.claimFollowUp);
    await userEvent.type(screen.getByRole("textbox", { name: /answer/i }), ANSWER);
    await userEvent.click(screen.getByTestId(testIds.claimFollowUpSubmit));
    await waitFor(() => expect(post).toHaveBeenCalledTimes(1));
    const body = post.mock.calls[0][1].body as { claimText: string };
    expect(body.claimText).toContain(CLAIM_TEXT);
    expect(body.claimText).toContain(QUESTION);
    expect(body.claimText).toContain(ANSWER);
    await waitFor(() => expect(push).toHaveBeenCalledWith(expect.stringContaining(NEW_CLAIM)));
  });

  it("does not post when the answer is blank", async () => {
    render(<ClaimResultView contractId={CONTRACT} claimId={CLAIM} />);
    await screen.findByTestId(testIds.claimFollowUp);
    await userEvent.type(screen.getByRole("textbox", { name: /answer/i }), "   ");
    await userEvent.click(screen.getByTestId(testIds.claimFollowUpSubmit));
    expect(post).not.toHaveBeenCalled();
  });
});

describe("composeFollowUpClaim", () => {
  it("keeps the composed text within 2000 characters", () => {
    const compose = (claimsApi as unknown as {
      composeFollowUpClaim: (c: string, q: string, a: string) => string;
    }).composeFollowUpClaim;
    expect(typeof compose).toBe("function");
    const out = compose("c".repeat(MAX), QUESTION, "a".repeat(MAX));
    expect(out.length).toBeLessThanOrEqual(MAX);
  });

  it("includes claim, question and answer when they fit", () => {
    const out = claimsApi.composeFollowUpClaim(CLAIM_TEXT, QUESTION, ANSWER);
    expect(out).toContain(CLAIM_TEXT);
    expect(out).toContain(QUESTION);
    expect(out).toContain(ANSWER);
    expect(out.length).toBeLessThanOrEqual(MAX);
  });
});
