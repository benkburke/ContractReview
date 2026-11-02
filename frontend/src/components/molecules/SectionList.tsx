import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemText from "@mui/material/ListItemText";
import type { components } from "@/api/generated/schema";
import { testIds } from "@/constants/testIds";
import { indexStrings } from "@/strings/contracts";

type Props = { sections: components["schemas"]["ContractSectionDto"][]; labelledBy: string };

export function SectionList({ sections, labelledBy }: Props) {
  return (
    <List dense aria-labelledby={labelledBy} data-testid={testIds.indexSections}>
      {sections.map((s) => {
        const count = Number(s.chunkCount);
        return (
          <ListItem key={Number(s.order)} divider data-testid={testIds.indexSectionItem}>
            <ListItemText
              primary={s.heading}
              secondary={`${count} ${count === 1 ? indexStrings.chunk : indexStrings.chunks}`}
            />
          </ListItem>
        );
      })}
    </List>
  );
}
