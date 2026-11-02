import Container from "@mui/material/Container";
import Typography from "@mui/material/Typography";
import { appStrings } from "@/strings/app";
import { testIds } from "@/constants/testIds";

export default function Home() {
  return (
    <Container component="main" maxWidth="md" sx={{ py: 6 }}>
      <Typography variant="h3" component="h1" data-testid={testIds.homeHeading}>
        {appStrings.welcome}
      </Typography>
      <Typography variant="body1" data-testid={testIds.homeDescription} sx={{ mt: 2 }}>
        {appStrings.intro}
      </Typography>
    </Container>
  );
}
