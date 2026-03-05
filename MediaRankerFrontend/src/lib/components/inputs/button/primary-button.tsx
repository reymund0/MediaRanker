import { Button } from "@mui/material";
import { ButtonProps } from "@mui/material";

export function PrimaryButton(props: ButtonProps) {
  return <Button {...props} variant="contained" />;
}
