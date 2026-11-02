import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import type { components } from "@/api/generated/schema";
import { ResultCard } from "@/components/molecules/ResultCard";
import { testIds } from "@/constants/testIds";
import { claimStrings } from "@/strings/claims";

type Citation = components["schemas"]["ClaimCitationDto"];

export function CitationsCard({
  citations,
}: {
  citations: readonly Citation[];
}) {
  return (
    <ResultCard
      heading={claimStrings.citations}
      testId={testIds.claimCitations}
    >
      {citations.length === 0 ? (
        <Typography color="text.secondary">
          {claimStrings.noCitations}
        </Typography>
      ) : (
        <Box
          component="ul"
          sx={{
            listStyle: "none",
            p: 0,
            m: 0,
            display: "flex",
            flexDirection: "column",
            gap: 2,
          }}
        >
          {citations.map((c, index) => (
            <li
              key={`${index}-${c.sectionHeading}`}
              data-testid={testIds.claimCitationItem}
            >
              <Typography variant="subtitle1" component="h3">
                {c.sectionHeading}
              </Typography>
              <Typography
                component="blockquote"
                sx={{
                  m: 0,
                  pl: 2,
                  borderLeft: 3,
                  borderColor: "divider",
                  whiteSpace: "pre-wrap",
                }}
              >
                {c.excerpt}
              </Typography>
            </li>
          ))}
        </Box>
      )}
    </ResultCard>
  );
}
