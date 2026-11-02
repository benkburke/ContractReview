import FormHelperText from "@mui/material/FormHelperText";

type Props = { id: string; message: string | null; testId: string };

export function FieldError({ id, message, testId }: Props) {
  if (!message) return null;
  return (
    <FormHelperText id={id} error role="alert" data-testid={testId}>
      {message}
    </FormHelperText>
  );
}
