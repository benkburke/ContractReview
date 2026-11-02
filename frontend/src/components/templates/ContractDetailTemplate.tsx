import Container from "@mui/material/Container";
import { ContractDetailView } from "@/components/organisms/ContractDetailView";

export function ContractDetailTemplate({ id }: { id: string }) {
  return (
    <Container component="main" maxWidth="md" sx={{ py: 6 }}>
      <ContractDetailView id={id} />
    </Container>
  );
}
