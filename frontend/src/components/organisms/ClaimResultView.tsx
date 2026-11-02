"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import FormControl from "@mui/material/FormControl";
import FormControlLabel from "@mui/material/FormControlLabel";
import FormLabel from "@mui/material/FormLabel";
import Paper from "@mui/material/Paper";
import Radio from "@mui/material/Radio";
import RadioGroup from "@mui/material/RadioGroup";
import Skeleton from "@mui/material/Skeleton";
import Typography from "@mui/material/Typography";
import { evaluateClaim } from "@/api/claims";
import { apiClient } from "@/api/client";
import type { components } from "@/api/generated/schema";
import { isClaimNotFound, readProblem } from "@/api/problem";
import { AppLink } from "@/components/atoms/AppLink";
import { ClaimStatusBanner } from "@/components/atoms/ClaimStatusBanner";
import { ConfidenceGauge } from "@/components/atoms/ConfidenceGauge";
import { CitationsCard } from "@/components/molecules/CitationsCard";
import { FieldError } from "@/components/molecules/FieldError";
import { ItemsCard } from "@/components/molecules/ItemsCard";
import { ResultCard } from "@/components/molecules/ResultCard";
import { claimFieldIds } from "@/constants/claims";
import { routes } from "@/constants/routes";
import { testIds } from "@/constants/testIds";
import { claimErrorStrings, claimStrings } from "@/strings/claims";

type Claim = components["schemas"]["ClaimEvaluationDto"];
type State =
  | { status: "loading" }
  | { status: "error" }
  | { status: "notFound" }
  | { status: "ready"; claim: Claim };

type Props = { contractId: string; claimId: string };

export function ClaimResultView({ contractId, claimId }: Props) {
  const router = useRouter();
  const [state, setState] = useState<State>({ status: "loading" });
  const [level, setLevel] = useState("");
  const [busy, setBusy] = useState(false);
  const [reError, setReError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    apiClient
      .GET("/api/contracts/{id}/claims/{claimId}", {
        params: { path: { id: contractId, claimId } },
      })
      .then(({ data, error, response }) => {
        if (!active) return;
        if (data) setState({ status: "ready", claim: data });
        else
          setState(
            isClaimNotFound(readProblem(error, response.status))
              ? { status: "notFound" }
              : { status: "error" },
          );
      })
      .catch(() => {
        if (active) setState({ status: "error" });
      });
    return () => {
      active = false;
    };
  }, [contractId, claimId]);

  const back = (
    <AppLink
      href={routes.contractDetail(contractId)}
      data-testid={testIds.claimBack}
    >
      {claimStrings.backToContract}
    </AppLink>
  );

  if (state.status === "loading") {
    return (
      <div
        role="status"
        aria-label={claimStrings.loading}
        data-testid={testIds.claimLoading}
      >
        <Skeleton variant="rectangular" height={240} />
      </div>
    );
  }
  if (state.status === "notFound") {
    return (
      <Box data-testid={testIds.claimNotFound}>
        <Typography variant="h4" component="h1" gutterBottom>
          {claimStrings.notFound}
        </Typography>
        <Typography sx={{ mb: 2 }}>{claimStrings.notFoundHelp}</Typography>
        {back}
      </Box>
    );
  }
  if (state.status === "error") {
    return (
      <Box>
        <Alert
          severity="error"
          sx={{ mb: 2 }}
          data-testid={testIds.claimLoadError}
        >
          {claimErrorStrings.load}
        </Alert>
        {back}
      </Box>
    );
  }

  const { claim } = state;
  const result = claim.result;

  async function reevaluate() {
    if (busy || !level) return;
    setBusy(true);
    setReError(null);
    const outcome = await evaluateClaim(contractId, claim.claimText, level);
    if (outcome.ok) {
      router.push(routes.claimDetail(contractId, outcome.claimId));
      return;
    }
    setReError(outcome.message);
    setBusy(false);
  }

  return (
    <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
      <Box>{back}</Box>
      <Typography variant="h4" component="h1">
        {claimStrings.resultHeading}
      </Typography>
      <ResultCard
        heading={claimStrings.claimTextHeading}
        testId={testIds.claimText}
      >
        <Typography sx={{ whiteSpace: "pre-wrap", overflowWrap: "anywhere" }}>
          {claim.claimText}
        </Typography>
      </ResultCard>
      {claim.state === "Failed" || !result ? (
        <Alert severity="error" data-testid={testIds.claimFailed}>
          <Typography component="p" sx={{ fontWeight: 600 }}>
            {claimStrings.failedHeading}
          </Typography>
          {claimErrorStrings.failedByCode}
        </Alert>
      ) : (
        <>
          <ClaimStatusBanner status={result.status} />
          <Box
            sx={{
              display: "flex",
              flexWrap: "wrap",
              alignItems: "center",
              gap: 4,
            }}
          >
            <ConfidenceGauge value={Number(result.confidence)} />
            <Typography data-testid={testIds.claimCoverageLevel}>
              {claimStrings.coverageLevelLine(
                result.coverageLevel ??
                  claim.coverageLevelRequested ??
                  claimStrings.coverageLevelNone,
              )}
            </Typography>
          </Box>
          <ResultCard
            heading={claimStrings.reasoning}
            testId={testIds.claimReasoning}
          >
            <Typography sx={{ whiteSpace: "pre-wrap" }}>
              {result.reasoning}
            </Typography>
          </ResultCard>
          <ItemsCard
            heading={claimStrings.coveredItems}
            items={result.coveredItems}
            testId={testIds.claimCoveredItems}
          />
          <ItemsCard
            heading={claimStrings.excludedItems}
            items={result.excludedItems}
            testId={testIds.claimExcludedItems}
          />
          <CitationsCard citations={result.citations} />
          {result.followUpQuestion ? (
            <Alert severity="info" data-testid={testIds.claimFollowUp}>
              <Typography component="p" sx={{ fontWeight: 600 }}>
                {claimStrings.followUp}
              </Typography>
              {result.followUpQuestion}
            </Alert>
          ) : null}
          {result.needsCoverageLevel ? (
            <Paper variant="outlined" sx={{ p: 2 }} aria-busy={busy}>
              <FormControl data-testid={testIds.claimLevelPicker}>
                <FormLabel id={claimFieldIds.levelPickerLabel}>
                  {claimStrings.pickerLabel}
                </FormLabel>
                <RadioGroup
                  aria-labelledby={claimFieldIds.levelPickerLabel}
                  value={level}
                  onChange={(e) => setLevel(e.target.value)}
                >
                  {result.coverageLevelOptions.map((option) => (
                    <FormControlLabel
                      key={option}
                      value={option}
                      control={<Radio />}
                      label={option}
                      disabled={busy}
                    />
                  ))}
                </RadioGroup>
              </FormControl>
              <FieldError
                id={claimFieldIds.reevaluateError}
                message={reError}
                testId={testIds.claimReevaluateError}
              />
              <Box sx={{ mt: 1 }}>
                <Button
                  variant="contained"
                  onClick={reevaluate}
                  disabled={busy || !level}
                  aria-busy={busy}
                  data-testid={testIds.claimReevaluate}
                >
                  {busy ? claimStrings.reevaluating : claimStrings.reevaluate}
                </Button>
              </Box>
              <div role="status">
                {busy ? (
                  <Typography color="text.secondary">
                    {claimStrings.waitNote}
                  </Typography>
                ) : null}
              </div>
            </Paper>
          ) : null}
        </>
      )}
    </Box>
  );
}
