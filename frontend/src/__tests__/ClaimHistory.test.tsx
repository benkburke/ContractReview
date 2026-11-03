import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ClaimHistory } from "@/components/organisms/ClaimHistory";
import { testIds } from "@/constants/testIds";

const get = vi.hoisted(() => vi.fn());
vi.mock("@/api/client", () => ({ apiClient: { GET: get } }));

const claim = (n: number, over: Record<string, unknown> = {}) => ({
  id: `id-${n}`,
  claimText: `Claim number ${n}`,
  state: "Evaluated",
  status: "Covered",
  confidence: 80,
  reviewAgrees: null,
  createdAtUtc: "2026-01-02T03:04:05Z",
  ...over,
});

const page = (items: unknown[], total: number, p = 1) => ({
  data: { items, page: p, pageSize: 20, totalCount: total },
  response: { status: 200 },
});

describe("ClaimHistory", () => {
  beforeEach(() => get.mockReset());
  afterEach(cleanup);

  it("renders rows as links with truncated text, status and confidence", async () => {
    const long = "x".repeat(150);
    get.mockResolvedValue(
      page(
        [
          claim(1, { claimText: long }),
          claim(2, { state: "Failed", status: null, confidence: null }),
        ],
        2,
      ),
    );
    render(<ClaimHistory contractId="c1" />);
    const rows = await screen.findAllByTestId(testIds.claimHistoryRow);
    expect(rows).toHaveLength(2);
    const links = screen.getAllByRole("link");
    expect(links[0]).toHaveAttribute("href", "/contracts/c1/claims/id-1");
    expect(links[0]).toHaveAccessibleName(new RegExp(`^${long}\\. Covered\\.`));
    expect(rows[0]).toHaveTextContent("x".repeat(120) + "…");
    expect(rows[0]).not.toHaveTextContent("x".repeat(121));
    expect(rows[0]).toHaveTextContent("80% confidence");
    expect(rows[1]).toHaveTextContent("Failed");
    expect(rows[1]).not.toHaveTextContent("confidence");
    expect(
      screen.queryByTestId(testIds.claimHistoryLoadMore),
    ).not.toBeInTheDocument();
  });

  it("shows the empty state", async () => {
    get.mockResolvedValue(page([], 0));
    render(<ClaimHistory contractId="c1" />);
    expect(await screen.findByText("No claims yet.")).toBeInTheDocument();
  });

  it("shows an error for a failed request", async () => {
    get.mockResolvedValue({
      error: { code: "contract.not_found" },
      response: { status: 404 },
    });
    render(<ClaimHistory contractId="c1" />);
    expect(
      await screen.findByTestId(testIds.claimHistoryError),
    ).toBeInTheDocument();
  });

  it("appends the next page, focuses the first new row and announces", async () => {
    get
      .mockResolvedValueOnce(page([claim(1), claim(2)], 3))
      .mockResolvedValueOnce(page([claim(3)], 3, 2));
    render(<ClaimHistory contractId="c1" />);
    await userEvent.click(
      await screen.findByTestId(testIds.claimHistoryLoadMore),
    );
    await waitFor(() =>
      expect(screen.getAllByTestId(testIds.claimHistoryRow)).toHaveLength(3),
    );
    const links = screen.getAllByRole("link");
    expect(links[2]).toHaveFocus();
    expect(screen.getByTestId(testIds.claimHistoryAnnounce)).toHaveTextContent(
      "1 more claim loaded",
    );
    expect(get.mock.calls[1][1].params.query).toEqual({
      page: 2,
      pageSize: 20,
    });
    expect(
      screen.queryByTestId(testIds.claimHistoryLoadMore),
    ).not.toBeInTheDocument();
  });
});
