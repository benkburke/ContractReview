export const routes = {
  home: "/",
  contracts: "/contracts",
  contractDetail: (id: string) => `/contracts/${encodeURIComponent(id)}`,
  claimDetail: (id: string, claimId: string) =>
    `/contracts/${encodeURIComponent(id)}/claims/${encodeURIComponent(claimId)}`,
} as const;
