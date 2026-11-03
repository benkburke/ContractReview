"use client";

import type { Ref } from "react";
import CancelIcon from "@mui/icons-material/Cancel";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import ErrorOutlinedIcon from "@mui/icons-material/ErrorOutlined";
import HelpIcon from "@mui/icons-material/Help";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import Box from "@mui/material/Box";
import ListItem from "@mui/material/ListItem";
import Typography from "@mui/material/Typography";
import type { ClaimSummary } from "@/api/claimHistory";
import { AppLink } from "@/components/atoms/AppLink";
import { claimHistoryLimits } from "@/constants/claims";
import { routes } from "@/constants/routes";
import { testIds } from "@/constants/testIds";
import { formatDateTime } from "@/lib/format";
import { claimHistoryStrings, claimStrings } from "@/strings/claims";

const statusIcons = {
  Covered: <CheckCircleIcon fontSize="small" color="success" />,
  NotCovered: <CancelIcon fontSize="small" color="error" />,
  Conditional: <WarningAmberIcon fontSize="small" color="warning" />,
  InsufficientInformation: <HelpIcon fontSize="small" color="info" />,
} as const;

function truncate(text: string): string {
  return text.length > claimHistoryLimits.textPreviewMax
    ? text.slice(0, claimHistoryLimits.textPreviewMax) +
        claimHistoryLimits.ellipsis
    : text;
}

type Props = {
  contractId: string;
  claim: ClaimSummary;
  linkRef?: Ref<HTMLAnchorElement>;
};

export function ClaimHistoryRow({ contractId, claim, linkRef }: Props) {
  const status = claim.state === "Failed" ? null : claim.status;
  const statusText = status
    ? claimStrings.status[status]
    : claimHistoryStrings.failed;
  const icon = status ? (
    statusIcons[status]
  ) : (
    <ErrorOutlinedIcon fontSize="small" color="error" />
  );
  const date = formatDateTime(claim.createdAtUtc);
  const confidence =
    status && claim.confidence !== null ? Number(claim.confidence) : null;
  return (
    <ListItem disableGutters divider data-testid={testIds.claimHistoryRow}>
      <AppLink
        ref={linkRef}
        href={routes.claimDetail(contractId, claim.id)}
        underline="hover"
        color="inherit"
        aria-label={claimHistoryStrings.rowLabel(
          claim.claimText,
          statusText,
          date,
        )}
        sx={{ display: "block", width: "100%", py: 0.5 }}
      >
        <Typography sx={{ overflowWrap: "anywhere" }}>
          {truncate(claim.claimText)}
        </Typography>
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            gap: 1.5,
            flexWrap: "wrap",
          }}
        >
          <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
            {icon}
            <Typography variant="body2">{statusText}</Typography>
          </Box>
          {confidence !== null && (
            <Typography variant="body2">
              {claimHistoryStrings.confidenceText(confidence)}
            </Typography>
          )}
          <Typography variant="body2" color="text.secondary">
            {date}
          </Typography>
        </Box>
      </AppLink>
    </ListItem>
  );
}
