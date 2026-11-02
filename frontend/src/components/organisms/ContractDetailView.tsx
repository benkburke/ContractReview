"use client";

import { useEffect, useState } from "react";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Paper from "@mui/material/Paper";
import Skeleton from "@mui/material/Skeleton";
import Typography from "@mui/material/Typography";
import { apiClient } from "@/api/client";
import type { components } from "@/api/generated/schema";
import { isNotFound, readProblem } from "@/api/problem";
import { AppLink } from "@/components/atoms/AppLink";
import { ContractMeta } from "@/components/molecules/ContractMeta";
import { routes } from "@/constants/routes";
import { testIds } from "@/constants/testIds";
import { contractErrorStrings, contractStrings } from "@/strings/contracts";

type Detail = components["schemas"]["ContractDetailDto"];
type State =
  | { status: "loading" }
  | { status: "error" }
  | { status: "notFound" }
  | { status: "ready"; contract: Detail };

export function ContractDetailView({ id }: { id: string }) {
  const [state, setState] = useState<State>({ status: "loading" });

  useEffect(() => {
    let active = true;
    apiClient
      .GET("/api/contracts/{id}", { params: { path: { id } } })
      .then(({ data, error, response }) => {
        if (!active) return;
        if (data) setState({ status: "ready", contract: data });
        else setState(isNotFound(readProblem(error, response.status)) ? { status: "notFound" } : { status: "error" });
      })
      .catch(() => {
        if (active) setState({ status: "error" });
      });
    return () => {
      active = false;
    };
  }, [id]);

  const back = (testId: string) => (
    <AppLink href={routes.contracts} data-testid={testId}>
      {contractStrings.backToList}
    </AppLink>
  );

  if (state.status === "loading") {
    return (
      <div role="status" aria-label={contractStrings.detailLoading} data-testid={testIds.contractDetailLoading}>
        <Skeleton variant="rectangular" height={240} />
      </div>
    );
  }
  if (state.status === "notFound") {
    return (
      <Box data-testid={testIds.contractNotFound}>
        <Typography variant="h4" component="h1" gutterBottom>
          {contractStrings.notFound}
        </Typography>
        <Typography sx={{ mb: 2 }}>{contractStrings.notFoundHelp}</Typography>
        {back(testIds.contractNotFoundBack)}
      </Box>
    );
  }
  if (state.status === "error") {
    return (
      <Box>
        <Alert severity="error" sx={{ mb: 2 }} data-testid={testIds.contractDetailError}>
          {contractErrorStrings.loadDetail}
        </Alert>
        {back(testIds.contractDetailBack)}
      </Box>
    );
  }
  const { contract } = state;
  return (
    <Box>
      <Box sx={{ mb: 2 }}>{back(testIds.contractDetailBack)}</Box>
      <Typography variant="h4" component="h1" gutterBottom data-testid={testIds.contractDetailTitle}>
        {contract.title}
      </Typography>
      <ContractMeta contract={contract} />
      <Typography variant="h6" component="h2" sx={{ mt: 3, mb: 1 }}>
        {contractStrings.extractedText}
      </Typography>
      <Paper
        variant="outlined"
        component="div"
        role="region"
        tabIndex={0}
        aria-label={contractStrings.extractedTextLabel}
        data-testid={testIds.contractDetailText}
        sx={{ p: 2, maxHeight: "60vh", overflow: "auto", whiteSpace: "pre-wrap", overflowWrap: "anywhere" }}
      >
        {contract.extractedText}
      </Paper>
    </Box>
  );
}
