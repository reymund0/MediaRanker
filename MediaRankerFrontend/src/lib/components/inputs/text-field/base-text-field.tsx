/*
<TextField
          fullWidth
          label="Username or Email"
          value={usernameOrEmail}
          onChange={(e) => setUsernameOrEmail(e.target.value)}
          required
          margin="normal"
          autoComplete="username"
        />
*/

import { TextField } from "@mui/material";
import { TextFieldProps } from "@mui/material";

export function BaseTextField(props: TextFieldProps) {
  return <TextField fullWidth {...props} />;
}
