import Alert from "@mui/material/Alert";
import CancelIcon from "@mui/icons-material/Cancel";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import HelpIcon from "@mui/icons-material/Help";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import type { components } from "@/api/generated/schema";
import { testIds } from "@/constants/testIds";
import { claimStrings } from "@/strings/claims";

type Status = components["schemas"]["ClaimStatus"];

const statusView = {
  Covered: {
    severity: "success",
    icon: <CheckCircleIcon fontSize="inherit" />,
  },
  NotCovered: { severity: "error", icon: <CancelIcon fontSize="inherit" /> },
  Conditional: {
    severity: "warning",
    icon: <WarningAmberIcon fontSize="inherit" />,
  },
  InsufficientInformation: {
    severity: "info",
    icon: <HelpIcon fontSize="inherit" />,
  },
} as const;

export function ClaimStatusBanner({ status }: { status: Status }) {
  const view = statusView[status];
  return (
    <Alert
      severity={view.severity}
      icon={view.icon}
      variant="filled"
      data-testid={testIds.claimStatusBanner}
      sx={{ fontSize: "1.1rem", fontWeight: 600 }}
    >
      {claimStrings.status[status]}
    </Alert>
  );
}
