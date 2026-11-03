import {
  act,
  cleanup,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ClaimResultView } from "@/components/organisms/ClaimResultView";
import { hubHeaders } from "@/constants/realtime";
import { routes } from "@/constants/routes";
import { testIds } from "@/constants/testIds";
import {
  claimProgressSteps,
  type ClaimProgressEvent,
} from "@/realtime/contracts";

const post = vi.hoisted(() => vi.fn());
const get = vi.hoisted(() => vi.fn());
const push = vi.hoisted(() => vi.fn());
vi.mock("@/api/client", () => ({ apiClient: { POST: post, GET: get } }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));

const hub = vi.hoisted(() => ({
  connected: false,
  startFails: false,
  handler: null as ((event: unknown) => void) | null,
  start: vi.fn(),
  stop: vi.fn(),
}));

vi.mock("@microsoft/signalr", () => {
  class HubConnectionBuilder {
    withUrl() {
      return this;
    }
    withAutomaticReconnect() {
      return this;
    }
    build() {
      return {
        get state() {
          return hub.connected ? "Connected" : "Disconnected";
        },
        connectionId: "conn-1",
        on: (_n: string, handler: (event: unknown) => void) => {
          hub.handler = handler;
        },
        off: () => {
          hub.handler = null;
        },
        start: hub.start,
        stop: hub.stop,
      };
    }
  }
  return {
    HubConnectionBuilder,
    HubConnectionState: { Connected: "Connected", Disconnected: "Disconnected" },
  };
});

const CONTRACT = "11111111-1111-4111-8111-111111111111";
const CLAIM = "22222222-2222-4222-8222-222222222222";
const NEW_CLAIM = "33333333-3333-4333-8333-333333333333";

function claimWith(result: Record<string, unknown>) {
  return {
    id: CLAIM,
    claimText: "Is the radiator covered?",
    state: "Completed",
    coverageLevelRequested: null,
    review: null,
    result: {
      status: "InsufficientInformation",
      confidence: 0.5,
      reasoning: "r",
      coveredItems: [],
      excludedItems: [],
      citations: [],
      coverageLevel: null,
      needsCoverageLevel: false,
      coverageLevelOptions: [],
      followUpQuestion: null,
      ...result,
    },
  };
}

function event(step: ClaimProgressEvent["step"]): ClaimProgressEvent {
  return {
    contractId: CONTRACT,
    step,
    occurredAtUtc: "2026-01-01T00:00:00Z",
    claimId: null,
    errorCode: null,
  };
}

function states() {
  return screen
    .getAllByTestId(testIds.claimProgressStep)
    .map((el) => el.getAttribute("data-state"));
}

async function answerFollowUp() {
  await userEvent.type(
    await screen.findByTestId(testIds.claimFollowUpAnswer),
    "Yes, 80k km",
  );
  await userEvent.click(screen.getByTestId(testIds.claimFollowUpSubmit));
}

async function evaluateAgain() {
  await screen.findByTestId(testIds.claimLevelPicker);
  await userEvent.click(screen.getByRole("radio", { name: "Gold" }));
  await userEvent.click(screen.getByTestId(testIds.claimReevaluate));
}

describe("ClaimResultView live progress", () => {
  let finish: (value: unknown) => void;

  beforeEach(() => {
    post.mockReset();
    get.mockReset();
    push.mockReset();
    hub.connected = true;
    hub.startFails = false;
    hub.handler = null;
    hub.start
      .mockReset()
      .mockImplementation(() =>
        hub.startFails ? Promise.reject(new Error("down")) : Promise.resolve(),
      );
    hub.stop.mockReset().mockResolvedValue(undefined);
    post.mockImplementation(() => new Promise((resolve) => (finish = resolve)));
  });
  afterEach(cleanup);

  function showFollowUp() {
    get.mockResolvedValue({
      data: claimWith({ followUpQuestion: "What is the mileage?" }),
      response: { status: 200 },
    });
    render(<ClaimResultView contractId={CONTRACT} claimId={CLAIM} />);
  }

  function showPicker() {
    get.mockResolvedValue({
      data: claimWith({
        needsCoverageLevel: true,
        coverageLevelOptions: ["Gold", "Silver"],
      }),
      response: { status: 200 },
    });
    render(<ClaimResultView contractId={CONTRACT} claimId={CLAIM} />);
  }

  it("sends the connection id and shows progress when answering a follow-up", async () => {
    showFollowUp();
    await answerFollowUp();
    expect(post.mock.calls[0][1].params.header).toEqual({
      [hubHeaders.connectionId]: "conn-1",
    });
    act(() => hub.handler?.(event(claimProgressSteps.retrieving)));
    expect(states()).toEqual(["current", "pending", "pending", "pending"]);
    act(() => hub.handler?.(event(claimProgressSteps.reasoning)));
    expect(states()).toEqual(["done", "current", "pending", "pending"]);

    finish({ data: { id: NEW_CLAIM }, response: { status: 201 } });
    await waitFor(() =>
      expect(push).toHaveBeenCalledWith(routes.claimDetail(CONTRACT, NEW_CLAIM)),
    );
  });

  it("sends the connection id and shows progress on Evaluate again", async () => {
    showPicker();
    await evaluateAgain();
    expect(post.mock.calls[0][1].params.header).toEqual({
      [hubHeaders.connectionId]: "conn-1",
    });
    act(() => hub.handler?.(event(claimProgressSteps.retrieving)));
    expect(states()).toEqual(["current", "pending", "pending", "pending"]);
    act(() => hub.handler?.(event(claimProgressSteps.reviewing)));
    expect(states()).toEqual(["done", "done", "current", "pending"]);
  });

  it("still submits a follow-up with no header and no error when the hub is down", async () => {
    hub.connected = false;
    hub.startFails = true;
    showFollowUp();
    await answerFollowUp();
    expect(post.mock.calls[0][1].params.header).toBeUndefined();
    finish({ data: { id: NEW_CLAIM }, response: { status: 201 } });
    await waitFor(() =>
      expect(push).toHaveBeenCalledWith(routes.claimDetail(CONTRACT, NEW_CLAIM)),
    );
    expect(
      screen.queryByTestId(testIds.claimFollowUpError),
    ).not.toBeInTheDocument();
  });

  it("still submits Evaluate again with no header and no error when the hub is down", async () => {
    hub.connected = false;
    hub.startFails = true;
    showPicker();
    await evaluateAgain();
    expect(post.mock.calls[0][1].params.header).toBeUndefined();
    finish({ data: { id: NEW_CLAIM }, response: { status: 201 } });
    await waitFor(() =>
      expect(push).toHaveBeenCalledWith(routes.claimDetail(CONTRACT, NEW_CLAIM)),
    );
    expect(
      screen.queryByTestId(testIds.claimReevaluateError),
    ).not.toBeInTheDocument();
  });
});
