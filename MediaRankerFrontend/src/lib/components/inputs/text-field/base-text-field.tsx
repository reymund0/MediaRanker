import { TextField } from "@mui/material";
import { TextFieldProps } from "@mui/material";

export function BaseTextField(props: TextFieldProps) {
  return <TextField fullWidth {...props} />;
}
