export const routes = {
  home: "/",
  contracts: "/contracts",
  contractDetail: (id: string) => `/contracts/${encodeURIComponent(id)}`,
} as const;
