import { Suspense } from "react";
import Container from "@mui/material/Container";
import Skeleton from "@mui/material/Skeleton";
import { ContractDetailTemplate } from "@/components/templates/ContractDetailTemplate";
import { contractStrings } from "@/strings/contracts";

async function ContractDetailContent({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <ContractDetailTemplate id={id} />;
}

export default function ContractDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  return (
    <Suspense
      fallback={
        <Container component="main" maxWidth="md" sx={{ py: 6 }}>
          <div role="status" aria-label={contractStrings.detailLoading}>
            <Skeleton variant="rectangular" height={240} />
          </div>
        </Container>
      }
    >
      <ContractDetailContent params={params} />
    </Suspense>
  );
}
