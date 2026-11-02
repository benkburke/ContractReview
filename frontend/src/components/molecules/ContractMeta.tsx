import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import type { components } from "@/api/generated/schema";
import { testIds } from "@/constants/testIds";
import { formatBytes, formatDateTime } from "@/lib/format";
import { contractStrings } from "@/strings/contracts";

type Props = { contract: components["schemas"]["ContractDetailDto"] };

export function ContractMeta({ contract }: Props) {
  const rows = [
    [
      contractStrings.fileName,
      contract.fileName,
      testIds.contractDetailFileName,
    ],
    [
      contractStrings.size,
      formatBytes(contract.sizeBytes),
      testIds.contractDetailSize,
    ],
    [
      contractStrings.pageCount,
      String(Number(contract.pageCount)),
      testIds.contractDetailPages,
    ],
    [
      contractStrings.uploaded,
      formatDateTime(contract.uploadedAtUtc),
      testIds.contractDetailUploaded,
    ],
  ] as const;
  return (
    <Box
      component="dl"
      sx={{
        m: 0,
        display: "grid",
        gridTemplateColumns: "max-content 1fr",
        columnGap: 2,
        rowGap: 0.5,
      }}
    >
      {rows.map(([label, value, testId]) => (
        <Box key={testId} sx={{ display: "contents" }}>
          <Typography component="dt" variant="body2" color="text.secondary">
            {label}
          </Typography>
          <Typography
            component="dd"
            variant="body1"
            sx={{ m: 0 }}
            data-testid={testId}
          >
            {value}
          </Typography>
        </Box>
      ))}
    </Box>
  );
}
