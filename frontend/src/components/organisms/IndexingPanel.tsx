"use client";

import { useState } from "react";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import CircularProgress from "@mui/material/CircularProgress";
import Typography from "@mui/material/Typography";
import { apiClient } from "@/api/client";
import type { components } from "@/api/generated/schema";
import { mapIndexProblem, readProblem } from "@/api/problem";
import { IndexStatusChip } from "@/components/atoms/IndexStatusChip";
import { SectionList } from "@/components/molecules/SectionList";
import { testIds } from "@/constants/testIds";
import { contractErrorStrings, indexStrings } from "@/strings/contracts";

type Detail = components["schemas"]["ContractDetailDto"];
type Props = { contract: Detail; onIndexed: (updated: Detail) => void };

const headingId = "index-sections-heading";

function messageFor(status: Detail["indexStatus"]): string | null {
  switch (status) {
    case "Failed":
      return indexStrings.failedMessage;
    case "NotIndexed":
      return indexStrings.notIndexedMessage;
    case "Indexing":
      return indexStrings.indexingMessage;
    default:
      return null;
  }
}

export function IndexingPanel({ contract, onIndexed }: Props) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  async function retry() {
    if (busy) return;
    setBusy(true);
    setError(null);
    setSuccess(false);
    try {
      const {
        data,
        error: problem,
        response,
      } = await apiClient.POST("/api/contracts/{id}/index", {
        params: { path: { id: contract.id } },
      });
      if (data) {
        onIndexed(data);
        setSuccess(true);
      } else {
        setError(mapIndexProblem(readProblem(problem, response.status)));
      }
    } catch {
      setError(contractErrorStrings.network);
    }
    setBusy(false);
  }

  const status = contract.indexStatus;
  const canRetry = status === "Failed" || status === "NotIndexed";
  const message = messageFor(status);

  return (
    <Box component="section" aria-labelledby={headingId} sx={{ mt: 3 }}>
      <Box sx={{ display: "flex", alignItems: "center" }}>
        <Typography id={headingId} variant="h6" component="h2">
          {indexStrings.sectionsHeading}
        </Typography>
        <IndexStatusChip status={status} />
      </Box>
      {status === "Indexed" && (
        <SectionList sections={contract.sections} labelledBy={headingId} />
      )}
      {message && (
        <Typography sx={{ my: 1 }} data-testid={testIds.indexMessage}>
          {message}
        </Typography>
      )}
      {canRetry && (
        <Button
          variant="outlined"
          onClick={retry}
          disabled={busy}
          aria-busy={busy}
          startIcon={
            busy ? (
              <CircularProgress size={16} color="inherit" aria-hidden />
            ) : undefined
          }
          data-testid={testIds.indexRetry}
        >
          {busy ? indexStrings.retrying : indexStrings.retry}
        </Button>
      )}
      <div role="status" data-testid={testIds.indexRetrySuccess}>
        {success ? indexStrings.retrySuccess : null}
      </div>
      {error && (
        <Alert
          severity="error"
          role="alert"
          sx={{ mt: 1 }}
          data-testid={testIds.indexRetryError}
        >
          {error}
        </Alert>
      )}
    </Box>
  );
}
