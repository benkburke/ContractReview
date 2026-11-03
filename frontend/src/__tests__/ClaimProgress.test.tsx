import {
  act,
  cleanup,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { StrictMode } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AskClaimForm } from "@/components/organisms/AskClaimForm";
import { errorCodes } from "@/constants/errorCodes";
import { hubHeaders, hubMethods, hubRoutes } from "@/constants/realtime";
import { routes } from "@/constants/routes";
import { testIds } from "@/constants/testIds";
import {
  claimProgressSteps,
  type ClaimProgressEvent,
} from "@/realtime/contracts";
import { claimErrorStrings, claimProgressStrings } from "@/strings/claims";

const post = vi.hoisted(() => vi.fn());
const push = vi.hoisted(() => vi.fn());
vi.mock("@/api/client", () => ({ apiClient: { POST: post, GET: vi.fn() } }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));

const hub = vi.hoisted(() => {
  const state = {
    connected: false,
    startFails: false,
    built: [] as unknown[],
    handler: null as ((event: unknown) => void) | null,
    url: "",
    start: vi.fn(),
    stop: vi.fn(),
  };
  return state;
});

vi.mock("@microsoft/signalr", () => {
  class HubConnectionBuilder {
    withUrl(url: string) {
      hub.url = url;
      return this;
    }
    withAutomaticReconnect() {
      return this;
    }
    build() {
      const connection = {
        get state() {
          return hub.connected ? "Connected" : "Disconnected";
        },
        connectionId: "conn-1",
        on: (_name: string, handler: (event: unknown) => void) => {
          hub.handler = handler;
        },
        off: () => {
          hub.handler = null;
        },
        start: hub.start,
        stop: hub.stop,
      };
      hub.built.push(connection);
      return connection;
    }
  }
  return {
    HubConnectionBuilder,
    HubConnectionState: {
      Connected: "Connected",
      Disconnected: "Disconnected",
    },
  };
});

const CONTRACT = "11111111-1111-4111-8111-111111111111";
const OTHER_CONTRACT = "99999999-9999-4999-8999-999999999999";
const CLAIM = "22222222-2222-4222-8222-222222222222";

function event(
  step: ClaimProgressEvent["step"],
  contractId = CONTRACT,
): ClaimProgressEvent {
  return {
    contractId,
    step,
    occurredAtUtc: "2026-01-01T00:00:00Z",
    claimId: null,
    errorCode: null,
  };
}

function push_(e: ClaimProgressEvent) {
  act(() => hub.handler?.(e));
}

async function submit() {
  await userEvent.type(
    screen.getByTestId(testIds.claimTextInput),
    "Is the radiator covered?",
  );
  await userEvent.click(screen.getByTestId(testIds.claimSubmit));
}

function states() {
  return screen
    .getAllByTestId(testIds.claimProgressStep)
    .map((el) => el.getAttribute("data-state"));
}

describe("AskClaimForm live progress", () => {
  let finish: (value: unknown) => void;

  beforeEach(() => {
    post.mockReset();
    push.mockReset();
    hub.connected = true;
    hub.startFails = false;
    hub.built = [];
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

  it("connects to the claim progress hub and stops on unmount", () => {
    const { unmount } = render(<AskClaimForm contractId={CONTRACT} />);
    expect(hub.url.endsWith(hubRoutes.claimProgress)).toBe(true);
    expect(hub.start).toHaveBeenCalledTimes(1);
    unmount();
    expect(hub.stop).toHaveBeenCalledTimes(1);
  });

  it("keeps a single connection under StrictMode double mount", () => {
    render(
      <StrictMode>
        <AskClaimForm contractId={CONTRACT} />
      </StrictMode>,
    );
    // A StrictMode remount may rebuild after the first release, but only one stays open.
    expect(hub.built.length - hub.stop.mock.calls.length).toBe(1);
    expect(hub.handler).not.toBeNull();
  });

  it("sends the connection id header and shows the steps in order", async () => {
    render(<AskClaimForm contractId={CONTRACT} />);
    await submit();
    expect(post.mock.calls[0][1].params.header).toEqual({
      [hubHeaders.connectionId]: "conn-1",
    });

    expect(screen.getByTestId(testIds.claimWaitNote)).toHaveAttribute(
      "aria-live",
      "polite",
    );
    push_(event(claimProgressSteps.retrieving));
    const region = screen.getByTestId(testIds.claimProgress);
    expect(
      within(region).getByText(claimProgressStrings.retrieving),
    ).toBeInTheDocument();
    expect(states()).toEqual(["current", "pending", "pending", "pending"]);

    push_(event(claimProgressSteps.reasoning));
    expect(states()).toEqual(["done", "current", "pending", "pending"]);

    push_(event(claimProgressSteps.reviewing));
    expect(states()).toEqual(["done", "done", "current", "pending"]);

    push_(event(claimProgressSteps.completed));
    expect(states()).toEqual(["done", "done", "done", "done"]);
    expect(
      within(region).getByText(claimProgressStrings.done),
    ).toBeInTheDocument();

    finish({ data: { id: CLAIM }, response: { status: 201 } });
    await waitFor(() =>
      expect(push).toHaveBeenCalledWith(routes.claimDetail(CONTRACT, CLAIM)),
    );
  });

  it("ignores events for another contract", async () => {
    render(<AskClaimForm contractId={CONTRACT} />);
    await submit();
    push_(event(claimProgressSteps.retrieving, OTHER_CONTRACT));
    expect(screen.queryByTestId(testIds.claimProgress)).toBeEmptyDOMElement();
  });

  it("shows the Failed step next to the evaluation error", async () => {
    render(<AskClaimForm contractId={CONTRACT} />);
    await submit();
    push_(event(claimProgressSteps.retrieving));
    push_(event(claimProgressSteps.reasoning));
    push_(event(claimProgressSteps.failed));
    finish({
      error: {
        code: errorCodes.claimEvaluationFailed,
        title: "x",
        status: 502,
      },
      response: { status: 502 },
    });
    expect(await screen.findByTestId(testIds.claimFormError)).toHaveTextContent(
      claimErrorStrings.evaluationFailed,
    );
    const region = screen.getByTestId(testIds.claimProgress);
    expect(
      within(region).getByText(claimProgressStrings.failed),
    ).toBeInTheDocument();
    expect(states()).toEqual(["done", "failed", "failed", "failed"]);
  });

  it("still submits, with no header and no error, when the hub is down", async () => {
    hub.connected = false;
    hub.startFails = true;
    render(<AskClaimForm contractId={CONTRACT} />);
    await submit();
    expect(post.mock.calls[0][1].params.header).toBeUndefined();
    expect(screen.getByTestId(testIds.claimSubmit)).toHaveAttribute(
      "aria-busy",
      "true",
    );
    expect(
      screen.queryByTestId(testIds.claimProgressStep),
    ).not.toBeInTheDocument();

    finish({ data: { id: CLAIM }, response: { status: 201 } });
    await waitFor(() =>
      expect(push).toHaveBeenCalledWith(routes.claimDetail(CONTRACT, CLAIM)),
    );
    expect(
      screen.queryByTestId(testIds.claimFormError),
    ).not.toBeInTheDocument();
  });
});
