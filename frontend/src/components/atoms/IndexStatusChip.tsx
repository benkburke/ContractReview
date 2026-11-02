import Chip from "@mui/material/Chip";
import type { components } from "@/api/generated/schema";
import { testIds } from "@/constants/testIds";
import { indexStrings } from "@/strings/contracts";

type Status = components["schemas"]["IndexStatus"];
const colors = {
  Indexed: "success",
  NotIndexed: "default",
  Indexing: "info",
  Failed: "error",
} as const;

export function IndexStatusChip({ status }: { status: Status }) {
  return (
    <Chip
      size="small"
      variant="outlined"
      color={colors[status]}
      label={indexStrings.status[status]}
      data-testid={testIds.indexStatusChip}
      sx={{ ml: 2, flexShrink: 0 }}
    />
  );
}
