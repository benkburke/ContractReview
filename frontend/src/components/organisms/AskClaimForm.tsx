"use client";

import { useState, type SyntheticEvent } from "react";
import { useRouter } from "next/navigation";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import CircularProgress from "@mui/material/CircularProgress";
import Typography from "@mui/material/Typography";
import TextField from "@mui/material/TextField";
import { evaluateClaim } from "@/api/claims";
import { FieldError } from "@/components/molecules/FieldError";
import { claimFieldIds, claimLimits } from "@/constants/claims";
import { routes } from "@/constants/routes";
import { testIds } from "@/constants/testIds";
import { claimErrorStrings, claimStrings } from "@/strings/claims";

type Errors = {
  text: string | null;
  coverage: string | null;
  form: string | null;
};
const noErrors: Errors = { text: null, coverage: null, form: null };

export function AskClaimForm({ contractId }: { contractId: string }) {
  const router = useRouter();
  const [text, setText] = useState("");
  const [coverage, setCoverage] = useState("");
  const [errors, setErrors] = useState<Errors>(noErrors);
  const [busy, setBusy] = useState(false);

  async function onSubmit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    const trimmed = text.trim();
    const level = coverage.trim();
    const next: Errors = {
      text:
        trimmed.length < claimLimits.textMin ||
        trimmed.length > claimLimits.textMax
          ? claimErrorStrings.textInvalid
          : null,
      coverage:
        level.length > claimLimits.coverageMax
          ? claimErrorStrings.coverageInvalid
          : null,
      form: null,
    };
    setErrors(next);
    if (next.text || next.coverage) return;

    setBusy(true);
    const outcome = await evaluateClaim(
      contractId,
      trimmed,
      level === "" ? null : level,
    );
    if (outcome.ok) {
      router.push(routes.claimDetail(contractId, outcome.claimId));
      setBusy(false);
      setText("");
      setCoverage("");
      setErrors(noErrors);
      return;
    }
    setErrors({ ...noErrors, [outcome.field]: outcome.message });
    setBusy(false);
  }

  const textDescribedBy = [
    claimFieldIds.textHelp,
    errors.text ? claimFieldIds.textError : null,
  ]
    .filter(Boolean)
    .join(" ");
  const coverageDescribedBy = [
    claimFieldIds.coverageHelp,
    errors.coverage ? claimFieldIds.coverageError : null,
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <Box
      component="form"
      noValidate
      onSubmit={onSubmit}
      aria-busy={busy}
      aria-labelledby={claimFieldIds.heading}
      data-testid={testIds.claimForm}
      sx={{ display: "flex", flexDirection: "column", gap: 2, mt: 3 }}
    >
      <Typography variant="h6" component="h2" id={claimFieldIds.heading}>
        {claimStrings.askHeading}
      </Typography>
      <TextField
        id={claimFieldIds.text}
        label={claimStrings.textLabel}
        value={text}
        onChange={(e) => setText(e.target.value)}
        multiline
        minRows={4}
        required
        disabled={busy}
        error={Boolean(errors.text)}
        helperText={`${claimStrings.textHelp} ${claimStrings.counter(text.length, claimLimits.textMax)}`}
        slotProps={{
          htmlInput: {
            "aria-describedby": textDescribedBy,
            "data-testid": testIds.claimTextInput,
          },
          formHelperText: { id: claimFieldIds.textHelp },
        }}
      />
      <FieldError
        id={claimFieldIds.textError}
        message={errors.text}
        testId={testIds.claimTextError}
      />
      <TextField
        id={claimFieldIds.coverage}
        label={claimStrings.coverageLabel}
        value={coverage}
        onChange={(e) => setCoverage(e.target.value)}
        disabled={busy}
        error={Boolean(errors.coverage)}
        helperText={claimStrings.coverageHelp}
        slotProps={{
          htmlInput: {
            "aria-describedby": coverageDescribedBy,
            "data-testid": testIds.claimCoverageInput,
          },
          formHelperText: { id: claimFieldIds.coverageHelp },
        }}
      />
      <FieldError
        id={claimFieldIds.coverageError}
        message={errors.coverage}
        testId={testIds.claimCoverageError}
      />
      <FieldError
        id={claimFieldIds.formError}
        message={errors.form}
        testId={testIds.claimFormError}
      />
      <Button
        type="submit"
        variant="contained"
        disabled={busy}
        aria-busy={busy}
        startIcon={
          busy ? (
            <CircularProgress size={16} color="inherit" aria-hidden />
          ) : undefined
        }
        data-testid={testIds.claimSubmit}
        sx={{ alignSelf: "flex-start" }}
      >
        {busy ? claimStrings.evaluating : claimStrings.evaluate}
      </Button>
      <div role="status" data-testid={testIds.claimWaitNote}>
        {busy ? (
          <Typography color="text.secondary">
            {claimStrings.waitNote}
          </Typography>
        ) : null}
      </div>
    </Box>
  );
}
