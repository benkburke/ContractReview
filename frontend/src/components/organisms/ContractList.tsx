"use client";

import { useEffect, useState } from "react";
import Alert from "@mui/material/Alert";
import List from "@mui/material/List";
import Skeleton from "@mui/material/Skeleton";
import Typography from "@mui/material/Typography";
import { apiClient } from "@/api/client";
import type { components } from "@/api/generated/schema";
import { ContractListItem } from "@/components/molecules/ContractListItem";
import { testIds } from "@/constants/testIds";
import { contractErrorStrings, contractStrings } from "@/strings/contracts";

type Contracts = components["schemas"]["ContractSummaryDto"][];
type State =
  | { status: "loading" }
  | { status: "error" }
  | { status: "ready"; items: Contracts };

export function ContractList() {
  const [state, setState] = useState<State>({ status: "loading" });

  useEffect(() => {
    let active = true;
    apiClient
      .GET("/api/contracts")
      .then(({ data }) => {
        if (active)
          setState(
            data ? { status: "ready", items: data } : { status: "error" },
          );
      })
      .catch(() => {
        if (active) setState({ status: "error" });
      });
    return () => {
      active = false;
    };
  }, []);

  if (state.status === "loading") {
    return (
      <div
        role="status"
        aria-label={contractStrings.listLoading}
        data-testid={testIds.contractsLoading}
      >
        <Skeleton variant="rectangular" height={120} />
      </div>
    );
  }
  if (state.status === "error") {
    return (
      <Alert severity="error" data-testid={testIds.contractsError}>
        {contractErrorStrings.loadList}
      </Alert>
    );
  }
  if (state.items.length === 0) {
    return (
      <Typography data-testid={testIds.contractsEmpty}>
        {contractStrings.empty}
      </Typography>
    );
  }
  return (
    <List
      aria-label={contractStrings.listHeading}
      data-testid={testIds.contractsList}
    >
      {state.items.map((c) => (
        <ContractListItem key={c.id} contract={c} />
      ))}
    </List>
  );
}
