import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import Typography from "@mui/material/Typography";
import { ResultCard } from "@/components/molecules/ResultCard";
import { claimStrings } from "@/strings/claims";

type Props = { heading: string; items: readonly string[]; testId: string };

export function ItemsCard({ heading, items, testId }: Props) {
  return (
    <ResultCard heading={heading} testId={testId}>
      {items.length === 0 ? (
        <Typography color="text.secondary">{claimStrings.noItems}</Typography>
      ) : (
        <List dense disablePadding sx={{ listStyleType: "disc", pl: 3 }}>
          {items.map((item, index) => (
            <ListItem
              key={`${index}-${item}`}
              disableGutters
              sx={{ display: "list-item" }}
            >
              {item}
            </ListItem>
          ))}
        </List>
      )}
    </ResultCard>
  );
}
