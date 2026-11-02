import Box from "@mui/material/Box";
import CircularProgress from "@mui/material/CircularProgress";
import Typography from "@mui/material/Typography";
import { claimLimits } from "@/constants/claims";
import { testIds } from "@/constants/testIds";
import { claimStrings } from "@/strings/claims";

export function ConfidenceGauge({ value }: { value: number }) {
  const clamped = Math.min(claimLimits.gaugeMax, Math.max(0, value));
  return (
    <Box
      role="img"
      aria-label={claimStrings.confidenceLabel(clamped)}
      data-testid={testIds.claimConfidence}
      sx={{ display: "inline-flex", alignItems: "center", gap: 2 }}
    >
      <Box sx={{ position: "relative", display: "inline-flex" }}>
        <CircularProgress
          variant="determinate"
          value={clamped}
          size={claimLimits.gaugeSize}
          aria-hidden
        />
        <Box
          sx={{
            position: "absolute",
            inset: 0,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
          }}
        >
          <Typography variant="h6" component="span">
            {clamped}
          </Typography>
        </Box>
      </Box>
      <Typography component="span">{claimStrings.confidence}</Typography>
    </Box>
  );
}
