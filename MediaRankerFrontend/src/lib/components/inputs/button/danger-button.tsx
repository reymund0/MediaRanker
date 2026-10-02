import { Button, ButtonProps } from "@mui/material";

export function DangerButton({ variant, ...props }: ButtonProps) {
  return <Button {...props} color="error" variant={variant ?? "contained"} />;
}
