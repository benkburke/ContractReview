"use client";

import { useEffect, useRef, useState } from "react";
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
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import { composeFollowUpClaim, evaluateClaim } from "@/api/claims";
import { apiClient } from "@/api/client";
import type { components } from "@/api/generated/schema";
import { isClaimNotFound, readProblem } from "@/api/problem";
import { AppLink } from "@/components/atoms/AppLink";
import { ClaimStatusBanner } from "@/components/atoms/ClaimStatusBanner";
import { ConfidenceGauge } from "@/components/atoms/ConfidenceGauge";
import { CitationsCard } from "@/components/molecules/CitationsCard";
import { ClaimProgressSteps } from "@/components/molecules/ClaimProgressSteps";
import { FieldError } from "@/components/molecules/FieldError";
import { ClaimReviewPanel } from "@/components/molecules/ClaimReviewPanel";
import { ItemsCard } from "@/components/molecules/ItemsCard";
import { ResultCard } from "@/components/molecules/ResultCard";
import { claimFieldIds, claimLimits } from "@/constants/claims";
import { routes } from "@/constants/routes";
import { testIds } from "@/constants/testIds";
import { claimProgressSteps } from "@/realtime/contracts";
import { useClaimProgress } from "@/realtime/useClaimProgress";
import { claimErrorStrings, claimStrings } from "@/strings/claims";

type Claim = components["schemas"]["ClaimEvaluationDto"];
type State =
  | { status: "loading" }
  | { status: "error" }
  | { status: "notFound" }
  | { status: "ready"; claim: Claim };

// MUI dims disabled helper text to text.disabled (2.67:1 on white); keep it at text.secondary (WCAG AA 4.5:1).
const disabledHelperSx = {
  "&.Mui-disabled": { color: "text.secondary" },
} as const;

type Props = { contractId: string; claimId: string };

export function ClaimResultView({ contractId, claimId }: Props) {
  const router = useRouter();
  const [state, setState] = useState<State>({ status: "loading" });
  const [level, setLevel] = useState("");
  const [busy, setBusy] = useState(false);
  const [reError, setReError] = useState<string | null>(null);
  const [answer, setAnswer] = useState("");
  const [answerError, setAnswerError] = useState<string | null>(null);
  const [reviewOpen, setReviewOpen] = useState(false);
  const reviewSummaryRef = useRef<HTMLDivElement>(null);
  const reviewPanelRef = useRef<HTMLDivElement>(null);
  const progress = useClaimProgress(contractId);

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
  const followUpText =
    result?.followUpQuestion ||
    (result?.status === "InsufficientInformation" && !result.needsCoverageLevel
      ? claimStrings.followUpFallback
      : null);

  const showFailedSteps =
    progress.seen[progress.seen.length - 1] === claimProgressSteps.failed;
  const steps =
    busy || showFailedSteps ? (
      <div data-testid={testIds.claimProgress}>
        <ClaimProgressSteps seen={progress.seen} />
      </div>
    ) : null;

  const review = claim.review ?? null;
  const reviewDisagrees =
    review?.state === "Completed" && review.agreesWithPrimary === false;

  function showReview() {
    setReviewOpen(true);
    reviewPanelRef.current?.scrollIntoView?.({ block: "start" });
    reviewSummaryRef.current?.focus();
  }

  async function reevaluate() {
    if (busy || !level) return;
    setBusy(true);
    setReError(null);
    progress.reset();
    const outcome = await evaluateClaim(
      contractId,
      claim.claimText,
      level,
      progress.getConnectionId(),
    );
    if (outcome.ok) {
      progress.reset();
      router.push(routes.claimDetail(contractId, outcome.claimId));
      setBusy(false);
      setLevel("");
      return;
    }
    setReError(outcome.message);
    setBusy(false);
  }

  async function submitAnswer() {
    if (busy || !followUpText) return;
    if (!answer.trim()) {
      setAnswerError(claimStrings.answerRequired);
      return;
    }
    setBusy(true);
    setAnswerError(null);
    progress.reset();
    const composed = composeFollowUpClaim(
      claim.claimText,
      followUpText,
      answer,
    );
    const outcome = await evaluateClaim(
      contractId,
      composed,
      claim.coverageLevelRequested ?? null,
      progress.getConnectionId(),
    );
    if (outcome.ok) {
      progress.reset();
      router.push(routes.claimDetail(contractId, outcome.claimId));
      setBusy(false);
      setAnswer("");
      return;
    }
    setAnswerError(outcome.message);
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
          {reviewDisagrees ? (
            <Alert
              severity="warning"
              role="none"
              data-testid={testIds.claimReviewDisagreeBanner}
              action={
                <Button
                  color="inherit"
                  size="small"
                  onClick={showReview}
                  data-testid={testIds.claimReviewDisagreeAction}
                >
                  {claimStrings.review.disagreeAction}
                </Button>
              }
            >
              {claimStrings.review.disagreeBanner}
            </Alert>
          ) : null}
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
          <ClaimReviewPanel
            review={review}
            needsMoreInfo={
              result.status === "InsufficientInformation" ||
              result.needsCoverageLevel
            }
            expanded={reviewOpen}
            onToggle={setReviewOpen}
            summaryRef={reviewSummaryRef}
            panelRef={reviewPanelRef}
          />
          {followUpText ? (
            <Alert severity="info" data-testid={testIds.claimFollowUp}>
              <Typography component="p" sx={{ fontWeight: 600 }}>
                {claimStrings.followUp}
              </Typography>
              {followUpText}
            </Alert>
          ) : null}
          {followUpText && !result.needsCoverageLevel ? (
            <Paper variant="outlined" sx={{ p: 2 }} aria-busy={busy}>
              <TextField
                id={claimFieldIds.answer}
                label={claimStrings.answerLabel}
                value={answer}
                onChange={(e) => setAnswer(e.target.value)}
                multiline
                minRows={3}
                fullWidth
                required
                disabled={busy}
                error={Boolean(answerError)}
                helperText={claimStrings.counter(
                  answer.length,
                  claimLimits.answerMax,
                )}
                slotProps={{
                  htmlInput: {
                    maxLength: claimLimits.answerMax,
                    "aria-describedby": answerError
                      ? `${claimFieldIds.answerError} ${claimFieldIds.answerHelp}`
                      : claimFieldIds.answerHelp,
                    "data-testid": testIds.claimFollowUpAnswer,
                  },
                  formHelperText: {
                    id: claimFieldIds.answerHelp,
                    sx: disabledHelperSx,
                  },
                }}
              />
              <FieldError
                id={claimFieldIds.answerError}
                message={answerError}
                testId={testIds.claimFollowUpError}
              />
              <Box sx={{ mt: 1 }}>
                <Button
                  variant="contained"
                  onClick={submitAnswer}
                  disabled={busy}
                  aria-busy={busy}
                  data-testid={testIds.claimFollowUpSubmit}
                >
                  {busy ? claimStrings.reevaluating : claimStrings.answerSubmit}
                </Button>
              </Box>
              <div role="status" aria-live="polite">
                {busy ? (
                  <Typography color="text.secondary">
                    {claimStrings.waitNote}
                  </Typography>
                ) : null}
                {steps}
              </div>
            </Paper>
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
              <div role="status" aria-live="polite">
                {busy ? (
                  <Typography color="text.secondary">
                    {claimStrings.waitNote}
                  </Typography>
                ) : null}
                {steps}
              </div>
            </Paper>
          ) : null}
        </>
      )}
    </Box>
  );
}
