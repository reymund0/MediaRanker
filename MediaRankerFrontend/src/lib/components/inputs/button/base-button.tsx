import { Button } from "@mui/material";
import { ButtonProps } from "@mui/material";

export function BaseButton(props: ButtonProps) {
  return <Button fullWidth {...props} />;
}
