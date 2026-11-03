"use client";

import { useCallback, useEffect, useState } from "react";
import {
  acquireClaimProgress,
  getClaimProgressConnectionId,
  subscribeClaimProgress,
} from "@/realtime/claimProgress";
import type { ClaimProgressStep } from "@/realtime/contracts";

/** Progress steps seen (in order) for one contract, plus the connection id to send with the submit. */
export function useClaimProgress(contractId: string) {
  const [seen, setSeen] = useState<readonly ClaimProgressStep[]>([]);

  useEffect(() => {
    const release = acquireClaimProgress();
    const unsubscribe = subscribeClaimProgress((event) => {
      if (event.contractId.toLowerCase() !== contractId.toLowerCase()) return;
      setSeen((previous) => [...previous, event.step]);
    });
    return () => {
      unsubscribe();
      release();
    };
  }, [contractId]);

  const reset = useCallback(() => setSeen([]), []);
  return { seen, reset, getConnectionId: getClaimProgressConnectionId };
}
