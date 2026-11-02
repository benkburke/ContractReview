import type { ReactNode } from "react";
import Paper from "@mui/material/Paper";
import Typography from "@mui/material/Typography";

type Props = { heading: string; testId: string; children: ReactNode };

export function ResultCard({ heading, testId, children }: Props) {
  return (
    <Paper
      variant="outlined"
      component="section"
      aria-label={heading}
      data-testid={testId}
      sx={{ p: 2 }}
    >
      <Typography variant="h6" component="h2" gutterBottom>
        {heading}
      </Typography>
      {children}
    </Paper>
  );
}
