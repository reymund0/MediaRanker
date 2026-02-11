// LinkBehavior.tsx
import NextLink from "next/link";
import * as React from "react";

export const LinkBehavior = React.forwardRef<
  HTMLAnchorElement,
  Omit<React.ComponentProps<typeof NextLink>, "href"> & {
    href: string;
  }
>(function LinkBehavior(props, ref) {
  const { href, ...other } = props;

  return (
    <NextLink ref={ref} href={href} {...other} />
  );
});
