import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { components } from "@/api/generated/schema";
import { IndexingPanel } from "@/components/organisms/IndexingPanel";
import { errorCodes } from "@/constants/errorCodes";
import { testIds } from "@/constants/testIds";
import { indexStrings } from "@/strings/contracts";

const post = vi.hoisted(() => vi.fn());
vi.mock("@/api/client", () => ({ apiClient: { POST: post } }));

type Detail = components["schemas"]["ContractDetailDto"];

function detail(
  indexStatus: Detail["indexStatus"],
  sections: Detail["sections"] = [],
): Detail {
  return {
    id: "11111111-1111-4111-8111-111111111111",
    title: "T",
    fileName: "a.pdf",
    sizeBytes: 1,
    pageCount: 1,
    uploadedAtUtc: "2026-01-01T00:00:00Z",
    extractedText: "x",
    indexStatus,
    sections,
  };
}

const indexed = detail("Indexed", [
  { order: 0, heading: "SCHEDULE OF COVERAGE", chunkCount: 2 },
  { order: 1, heading: "WHAT IS NOT COVERED", chunkCount: 1 },
]);

describe("IndexingPanel", () => {
  beforeEach(() => post.mockReset());
  afterEach(cleanup);

  it("shows the Indexed chip and the sections without a retry button", () => {
    render(<IndexingPanel contract={indexed} onIndexed={vi.fn()} />);

    expect(screen.getByTestId(testIds.indexStatusChip)).toHaveTextContent(
      indexStrings.status.Indexed,
    );
    expect(screen.getAllByTestId(testIds.indexSectionItem)).toHaveLength(2);
    expect(screen.queryByTestId(testIds.indexRetry)).not.toBeInTheDocument();
  });

  it("shows the failed message and retry button for a Failed contract", () => {
    render(<IndexingPanel contract={detail("Failed")} onIndexed={vi.fn()} />);

    expect(screen.getByTestId(testIds.indexStatusChip)).toHaveTextContent(
      indexStrings.status.Failed,
    );
    expect(screen.getByTestId(testIds.indexMessage)).toHaveTextContent(
      indexStrings.failedMessage,
    );
    expect(screen.getByTestId(testIds.indexRetry)).toHaveTextContent(
      indexStrings.retry,
    );
  });

  it("calls onIndexed and announces success when the retry succeeds", async () => {
    post.mockResolvedValue({ data: indexed, response: { status: 200 } });
    const onIndexed = vi.fn();
    render(<IndexingPanel contract={detail("Failed")} onIndexed={onIndexed} />);

    await userEvent.click(screen.getByTestId(testIds.indexRetry));

    await waitFor(() => expect(onIndexed).toHaveBeenCalledWith(indexed));
    expect(screen.getByTestId(testIds.indexRetrySuccess)).toHaveTextContent(
      indexStrings.retrySuccess,
    );
    expect(
      screen.queryByTestId(testIds.indexRetryError),
    ).not.toBeInTheDocument();
  });

  it("shows the embedding error in an alert when the retry fails", async () => {
    post.mockResolvedValue({
      error: { code: errorCodes.embeddingFailed, status: 502 },
      response: { status: 502 },
    });
    const onIndexed = vi.fn();
    render(<IndexingPanel contract={detail("Failed")} onIndexed={onIndexed} />);

    await userEvent.click(screen.getByTestId(testIds.indexRetry));

    const alert = await screen.findByTestId(testIds.indexRetryError);
    expect(alert).toHaveTextContent(indexStrings.embeddingFailed);
    expect(alert).toHaveAttribute("role", "alert");
    expect(onIndexed).not.toHaveBeenCalled();
    expect(screen.getByTestId(testIds.indexRetry)).toBeEnabled();
  });

  it("shows the in-progress message on a 409 conflict", async () => {
    post.mockResolvedValue({
      error: { code: errorCodes.indexingInProgress, status: 409 },
      response: { status: 409 },
    });
    render(
      <IndexingPanel contract={detail("NotIndexed")} onIndexed={vi.fn()} />,
    );

    await userEvent.click(screen.getByTestId(testIds.indexRetry));

    expect(
      await screen.findByTestId(testIds.indexRetryError),
    ).toHaveTextContent(indexStrings.inProgress);
  });

  it("disables the button and sets aria-busy while the request runs", async () => {
    let resolve: (v: unknown) => void = () => {};
    post.mockReturnValue(new Promise((r) => (resolve = r)));
    render(<IndexingPanel contract={detail("Failed")} onIndexed={vi.fn()} />);

    await userEvent.click(screen.getByTestId(testIds.indexRetry));

    const button = screen.getByTestId(testIds.indexRetry);
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute("aria-busy", "true");
    resolve({ data: indexed, response: { status: 200 } });
    await waitFor(() =>
      expect(screen.getByTestId(testIds.indexRetry)).toBeEnabled(),
    );
  });
});
