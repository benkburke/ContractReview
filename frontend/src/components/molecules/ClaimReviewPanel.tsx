"use client";

import type { Ref } from "react";
import Accordion from "@mui/material/Accordion";
import AccordionDetails from "@mui/material/AccordionDetails";
import AccordionSummary from "@mui/material/AccordionSummary";
import Box from "@mui/material/Box";
import LinearProgress from "@mui/material/LinearProgress";
import Typography from "@mui/material/Typography";
import CancelOutlined from "@mui/icons-material/CancelOutlined";
import CheckCircleOutlined from "@mui/icons-material/CheckCircleOutlined";
import ExpandMore from "@mui/icons-material/ExpandMore";
import type { components } from "@/api/generated/schema";
import { testIds } from "@/constants/testIds";
import { claimStrings } from "@/strings/claims";

type Review = components["schemas"]["ClaimReviewDto"];

const reviewStrings = claimStrings.review;
const summaryId = "claim-review-summary-id";
const contentId = "claim-review-content-id";

type Props = {
  review: Review | null;
  /** True when a null review is expected because the primary answer asked for more information. */
  needsMoreInfo: boolean;
  expanded: boolean;
  onToggle: (expanded: boolean) => void;
  summaryRef: Ref<HTMLDivElement>;
  panelRef: Ref<HTMLDivElement>;
};

function List({
  heading,
  items,
  testId,
}: {
  heading: string;
  items: readonly string[];
  testId: string;
}) {
  return (
    <Box data-testid={testId}>
      <Typography variant="subtitle2" component="h3">
        {heading}
      </Typography>
      {items.length ? (
        <ul>
          {items.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      ) : (
        <Typography color="text.secondary">{claimStrings.noItems}</Typography>
      )}
    </Box>
  );
}

function Body({
  review,
  needsMoreInfo,
}: Pick<Props, "review" | "needsMoreInfo">) {
  if (!review) {
    return (
      <Typography data-testid={testIds.claimReviewNote}>
        {needsMoreInfo
          ? reviewStrings.notReviewedMoreInfo
          : reviewStrings.notReviewed}
      </Typography>
    );
  }
  if (review.state === "Failed") {
    return (
      <Typography data-testid={testIds.claimReviewNote}>
        {reviewStrings.unavailable}
      </Typography>
    );
  }
  const confidence = Number(review.confidence ?? 0);
  const agrees = review.agreesWithPrimary === true;
  return (
    <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
      <Box
        sx={{ display: "flex", alignItems: "center", gap: 1 }}
        data-testid={testIds.claimReviewAgreement}
      >
        {agrees ? (
          <CheckCircleOutlined color="success" aria-hidden />
        ) : (
          <CancelOutlined color="warning" aria-hidden />
        )}
        <Typography sx={{ fontWeight: 600 }}>
          {agrees ? reviewStrings.agrees : reviewStrings.disagrees}
        </Typography>
      </Box>
      <Box data-testid={testIds.claimReviewConfidence}>
        <Typography>
          {reviewStrings.confidenceLine(confidence)}
        </Typography>
        <LinearProgress
          variant="determinate"
          value={confidence}
          aria-label={reviewStrings.confidenceLabel}
        />
      </Box>
      <Typography data-testid={testIds.claimReviewCovered}>
        {reviewStrings.coveredLine(
          review.isCovered === null
            ? reviewStrings.unknown
            : review.isCovered
              ? reviewStrings.yes
              : reviewStrings.no,
        )}
      </Typography>
      <Box data-testid={testIds.claimReviewExplanation}>
        <Typography variant="subtitle2" component="h3">
          {reviewStrings.explanation}
        </Typography>
        <Typography sx={{ whiteSpace: "pre-wrap" }}>
          {review.accuracyExplanation}
        </Typography>
      </Box>
      <List
        heading={reviewStrings.coveredItems}
        items={review.coveredItems}
        testId={testIds.claimReviewCoveredItems}
      />
      <List
        heading={reviewStrings.notCoveredItems}
        items={review.notCoveredItems}
        testId={testIds.claimReviewNotCoveredItems}
      />
      <Typography data-testid={testIds.claimReviewModel}>
        {reviewStrings.modelLine(review.model ?? reviewStrings.unknown)}
      </Typography>
    </Box>
  );
}

/** Collapsed "Debug / Evaluation" accordion showing the second-pass review. */
export function ClaimReviewPanel({
  review,
  needsMoreInfo,
  expanded,
  onToggle,
  summaryRef,
  panelRef,
}: Props) {
  return (
    <Accordion
      ref={panelRef}
      expanded={expanded}
      onChange={(_, value) => onToggle(value)}
      slotProps={{ heading: { component: "h2" } }}
      data-testid={testIds.claimReviewPanel}
    >
      <AccordionSummary
        ref={summaryRef}
        id={summaryId}
        aria-controls={contentId}
        expandIcon={<ExpandMore />}
        data-testid={testIds.claimReviewSummary}
      >
        <Typography component="span">{reviewStrings.heading}</Typography>
      </AccordionSummary>
      <AccordionDetails id={contentId}>
        <Body review={review} needsMoreInfo={needsMoreInfo} />
      </AccordionDetails>
    </Accordion>
  );
}
