import Container from "@mui/material/Container";
import { ClaimResultView } from "@/components/organisms/ClaimResultView";

type Props = { contractId: string; claimId: string };

export function ClaimResultTemplate({ contractId, claimId }: Props) {
  return (
    <Container component="main" maxWidth="md" sx={{ py: 6 }}>
      <ClaimResultView contractId={contractId} claimId={claimId} />
    </Container>
  );
}
