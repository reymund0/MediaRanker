import Link from "next/link";
import { Link as MuiLink } from "@mui/material";

export function NextLink(props: React.ComponentProps<typeof Link>) {
  return <MuiLink {...props} component={Link} />;
}
