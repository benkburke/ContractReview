import Container from "@mui/material/Container";
import Typography from "@mui/material/Typography";
import { ContractList } from "@/components/organisms/ContractList";
import { UploadContractForm } from "@/components/organisms/UploadContractForm";
import { contractStrings } from "@/strings/contracts";

export function ContractsTemplate() {
  return (
    <Container component="main" maxWidth="md" sx={{ py: 6 }}>
      <Typography variant="h3" component="h1" gutterBottom>
        {contractStrings.pageTitle}
      </Typography>
      <Typography variant="h5" component="h2" gutterBottom>
        {contractStrings.uploadHeading}
      </Typography>
      <UploadContractForm />
      <Typography variant="h5" component="h2" sx={{ mt: 5 }}>
        {contractStrings.listHeading}
      </Typography>
      <ContractList />
    </Container>
  );
}
