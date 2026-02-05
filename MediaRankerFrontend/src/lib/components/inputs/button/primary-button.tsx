import { BaseButton } from "./base-button";
import { ButtonProps } from "@mui/material";

export function PrimaryButton(props: ButtonProps) {
  return <BaseButton {...props} variant="contained" />;
}
