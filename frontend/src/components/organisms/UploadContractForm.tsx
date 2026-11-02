"use client";

import { useState, type SyntheticEvent } from "react";
import { useRouter } from "next/navigation";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import CircularProgress from "@mui/material/CircularProgress";
import FormControl from "@mui/material/FormControl";
import FormHelperText from "@mui/material/FormHelperText";
import FormLabel from "@mui/material/FormLabel";
import TextField from "@mui/material/TextField";
import { apiClient } from "@/api/client";
import { mapUploadProblem, readProblem } from "@/api/problem";
import { FieldError } from "@/components/molecules/FieldError";
import { routes } from "@/constants/routes";
import { testIds } from "@/constants/testIds";
import {
  uploadFieldIds,
  uploadFormFields,
  uploadLimits,
} from "@/constants/upload";
import { validateFile, validateTitle } from "@/lib/uploadValidation";
import { contractErrorStrings, contractStrings } from "@/strings/contracts";

type Errors = {
  title: string | null;
  file: string | null;
  form: string | null;
};
const noErrors: Errors = { title: null, file: null, form: null };

export function UploadContractForm() {
  const router = useRouter();
  const [title, setTitle] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [errors, setErrors] = useState<Errors>(noErrors);
  const [busy, setBusy] = useState(false);

  async function onSubmit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    const next: Errors = {
      title: validateTitle(title),
      file: validateFile(file),
      form: null,
    };
    setErrors(next);
    if (next.title || next.file || !file) return;

    setBusy(true);
    try {
      const { data, error, response } = await apiClient.POST("/api/contracts", {
        body: { title: title.trim(), file: file as unknown as string },
        bodySerializer: (body) => {
          const form = new FormData();
          form.append(uploadFormFields.title, body.title);
          form.append(uploadFormFields.file, file);
          return form;
        },
      });
      if (data) {
        router.push(routes.contractDetail(data.id));
        setBusy(false);
        return;
      }
      const mapped = mapUploadProblem(readProblem(error, response.status));
      setErrors({ ...noErrors, [mapped.field]: mapped.message });
    } catch {
      setErrors({ ...noErrors, form: contractErrorStrings.network });
    }
    setBusy(false);
  }

  const titleDescribedBy = errors.title ? uploadFieldIds.titleError : undefined;
  const fileDescribedBy = errors.file ? uploadFieldIds.fileError : undefined;

  return (
    <Box
      component="form"
      noValidate
      onSubmit={onSubmit}
      aria-busy={busy}
      data-testid={testIds.uploadForm}
      sx={{ display: "flex", flexDirection: "column", gap: 2, maxWidth: 520 }}
    >
      <TextField
        id={uploadFieldIds.title}
        label={contractStrings.titleLabel}
        value={title}
        onChange={(e) => setTitle(e.target.value)}
        required
        disabled={busy}
        error={Boolean(errors.title)}
        slotProps={{
          htmlInput: {
            "aria-describedby": titleDescribedBy,
            "data-testid": testIds.uploadTitleInput,
          },
        }}
      />
      <FieldError
        id={uploadFieldIds.titleError}
        message={errors.title}
        testId={testIds.uploadTitleError}
      />

      <FormControl error={Boolean(errors.file)}>
        <FormLabel htmlFor={uploadFieldIds.file} required>
          {contractStrings.fileLabel}
        </FormLabel>
        <input
          id={uploadFieldIds.file}
          type="file"
          accept={uploadLimits.accept}
          disabled={busy}
          required
          aria-invalid={Boolean(errors.file)}
          aria-describedby={fileDescribedBy}
          data-testid={testIds.uploadFileInput}
          onChange={(e) => setFile(e.target.files?.[0] ?? null)}
        />
        <FormHelperText>{contractStrings.fileHelp}</FormHelperText>
      </FormControl>
      <FieldError
        id={uploadFieldIds.fileError}
        message={errors.file}
        testId={testIds.uploadFileError}
      />
      <FieldError
        id={uploadFieldIds.formError}
        message={errors.form}
        testId={testIds.uploadFormError}
      />

      <Button
        type="submit"
        variant="contained"
        disabled={busy}
        aria-busy={busy}
        startIcon={
          busy ? (
            <CircularProgress
              size={16}
              color="inherit"
              aria-label={contractStrings.submitting}
            />
          ) : undefined
        }
        data-testid={testIds.uploadSubmit}
        sx={{ alignSelf: "flex-start" }}
      >
        {busy ? contractStrings.submitting : contractStrings.submit}
      </Button>
    </Box>
  );
}
