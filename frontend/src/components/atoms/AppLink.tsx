"use client";

import Link, { type LinkProps } from "@mui/material/Link";
import NextLink from "@/components/atoms/NextLink";

type Props = Omit<LinkProps<typeof NextLink>, "component">;

export function AppLink(props: Props) {
  return <Link component={NextLink} {...props} />;
}
