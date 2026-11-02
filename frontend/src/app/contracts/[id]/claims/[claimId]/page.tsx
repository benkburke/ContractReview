import { Suspense } from "react";
import Container from "@mui/material/Container";
import Skeleton from "@mui/material/Skeleton";
import { ClaimResultTemplate } from "@/components/templates/ClaimResultTemplate";
import { claimStrings } from "@/strings/claims";

type Params = Promise<{ id: string; claimId: string }>;

async function ClaimContent({ params }: { params: Params }) {
  const { id, claimId } = await params;
  return <ClaimResultTemplate contractId={id} claimId={claimId} />;
}

export default function ClaimPage({ params }: { params: Params }) {
  return (
    <Suspense
      fallback={
        <Container component="main" maxWidth="md" sx={{ py: 6 }}>
          <div role="status" aria-label={claimStrings.loading}>
            <Skeleton variant="rectangular" height={240} />
          </div>
        </Container>
      }
    >
      <ClaimContent params={params} />
    </Suspense>
  );
}
