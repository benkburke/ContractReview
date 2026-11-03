"use client";

import { useEffect, useRef, useState } from "react";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import List from "@mui/material/List";
import Skeleton from "@mui/material/Skeleton";
import Typography from "@mui/material/Typography";
import { fetchClaimHistory, type ClaimSummary } from "@/api/claimHistory";
import { ClaimHistoryRow } from "@/components/molecules/ClaimHistoryRow";
import { claimHistoryLimits } from "@/constants/claims";
import { testIds } from "@/constants/testIds";
import { claimHistoryStrings } from "@/strings/claims";

const visuallyHidden = {
  position: "absolute",
  width: 1,
  height: 1,
  overflow: "hidden",
  clip: "rect(0 0 0 0)",
  whiteSpace: "nowrap",
} as const;

type State =
  | { status: "loading" }
  | { status: "error"; message: string }
  | { status: "ready"; items: ClaimSummary[]; page: number; total: number };

export function ClaimHistory({ contractId }: { contractId: string }) {
  const [state, setState] = useState<State>({ status: "loading" });
  const [loadingMore, setLoadingMore] = useState(false);
  const [moreError, setMoreError] = useState<string | null>(null);
  const [announcement, setAnnouncement] = useState("");
  const pendingFocus = useRef<number | null>(null);

  useEffect(() => {
    let active = true;
    fetchClaimHistory(contractId, claimHistoryLimits.firstPage).then(
      (outcome) => {
        if (!active) return;
        setState(
          outcome.ok
            ? {
                status: "ready",
                items: outcome.page.items,
                page: Number(outcome.page.page),
                total: Number(outcome.page.totalCount),
              }
            : { status: "error", message: outcome.message },
        );
      },
    );
    return () => {
      active = false;
    };
  }, [contractId]);

  const focusIfPending = (index: number) => (el: HTMLAnchorElement | null) => {
    if (el && pendingFocus.current === index) {
      pendingFocus.current = null;
      el.focus();
    }
  };

  const loadMore = async () => {
    if (state.status !== "ready") return;
    setLoadingMore(true);
    setMoreError(null);
    const outcome = await fetchClaimHistory(contractId, state.page + 1);
    setLoadingMore(false);
    if (!outcome.ok) {
      setMoreError(outcome.message);
      return;
    }
    const added = outcome.page.items;
    pendingFocus.current = state.items.length;
    setAnnouncement(claimHistoryStrings.moreLoaded(added.length));
    setState({
      status: "ready",
      items: [...state.items, ...added],
      page: state.page + 1,
      total: Number(outcome.page.totalCount),
    });
  };

  let body;
  if (state.status === "loading") {
    body = (
      <div
        role="status"
        aria-label={claimHistoryStrings.loading}
        data-testid={testIds.claimHistoryLoading}
      >
        <Skeleton variant="rectangular" height={96} />
      </div>
    );
  } else if (state.status === "error") {
    body = (
      <Alert severity="error" data-testid={testIds.claimHistoryError}>
        {state.message}
      </Alert>
    );
  } else if (state.items.length === 0) {
    body = (
      <Typography data-testid={testIds.claimHistoryEmpty}>
        {claimHistoryStrings.empty}
      </Typography>
    );
  } else {
    body = (
      <>
        <List
          disablePadding
          aria-labelledby={claimHistoryLimits.headingId}
          data-testid={testIds.claimHistoryList}
        >
          {state.items.map((claim, index) => (
            <ClaimHistoryRow
              key={claim.id}
              contractId={contractId}
              claim={claim}
              linkRef={focusIfPending(index)}
            />
          ))}
        </List>
        {moreError && (
          <Alert severity="error" sx={{ mt: 1 }}>
            {moreError}
          </Alert>
        )}
        {state.items.length < state.total && (
          <Button
            variant="outlined"
            onClick={loadMore}
            disabled={loadingMore}
            sx={{ mt: 1 }}
            data-testid={testIds.claimHistoryLoadMore}
          >
            {loadingMore
              ? claimHistoryStrings.loadingMore
              : claimHistoryStrings.loadMore}
          </Button>
        )}
      </>
    );
  }

  return (
    <Box component="section" sx={{ mt: 3 }} data-testid={testIds.claimHistory}>
      <Typography
        variant="h6"
        component="h2"
        id={claimHistoryLimits.headingId}
        sx={{ mb: 1 }}
      >
        {claimHistoryStrings.heading}
      </Typography>
      {body}
      <Box
        role="status"
        data-testid={testIds.claimHistoryAnnounce}
        sx={visuallyHidden}
      >
        {announcement}
      </Box>
    </Box>
  );
}
