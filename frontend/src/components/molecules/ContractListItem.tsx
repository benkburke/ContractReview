import ListItem from "@mui/material/ListItem";
import ListItemText from "@mui/material/ListItemText";
import type { components } from "@/api/generated/schema";
import { IndexStatusChip } from "@/components/atoms/IndexStatusChip";
import { AppLink } from "@/components/atoms/AppLink";
import { routes } from "@/constants/routes";
import { testIds } from "@/constants/testIds";
import { formatDateTime } from "@/lib/format";
import { contractStrings } from "@/strings/contracts";

type Props = { contract: components["schemas"]["ContractSummaryDto"] };

export function ContractListItem({ contract }: Props) {
  const secondary = [
    contract.fileName,
    `${Number(contract.pageCount)} ${contractStrings.pages}`,
    formatDateTime(contract.uploadedAtUtc),
  ].join(contractStrings.itemSeparator);
  return (
    <ListItem divider data-testid={testIds.contractItem}>
      <ListItemText
        primary={
          <AppLink href={routes.contractDetail(contract.id)} data-testid={testIds.contractItemLink}>
            {contract.title}
          </AppLink>
        }
        secondary={secondary}
      />
      <IndexStatusChip status={contract.indexStatus} />
    </ListItem>
  );
}
