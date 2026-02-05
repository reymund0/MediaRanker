import { BaseButton } from "./base-button";
import { ButtonProps } from "@mui/material";

export function SecondaryButton(props: ButtonProps) {
  return <BaseButton {...props} variant="outlined" />;
}
