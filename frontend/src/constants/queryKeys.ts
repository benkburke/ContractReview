export const queryKeys = {
  contracts: ["contracts"],
  contract: (id: string) => ["contracts", id],
} as const;
