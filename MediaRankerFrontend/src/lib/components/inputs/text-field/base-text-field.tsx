"use client";

import Visibility from "@mui/icons-material/Visibility";
import VisibilityOff from "@mui/icons-material/VisibilityOff";
import {
  Box,
  IconButton,
  InputAdornment,
  TextField,
  TextFieldProps,
  Typography,
} from "@mui/material";
import { ReactNode, useId, useState } from "react";

export type BaseTextFieldProps = TextFieldProps & {
  labelAbove?: boolean;
};

export function BaseTextField({
  id: suppliedId,
  label,
  labelAbove = false,
  required,
  type,
  InputProps,
  slotProps,
  ...props
}: BaseTextFieldProps) {
  const generatedId = useId();
  const id = suppliedId ?? generatedId;
  const isPassword = type === "password";
  const [showPassword, setShowPassword] = useState(false);

  const resolvedSlotProps: TextFieldProps["slotProps"] = isPassword
    ? {
        ...slotProps,
        input: (ownerState) => {
          const providedInputProps = slotProps?.input;
          const resolvedInputProps =
            typeof providedInputProps === "function"
              ? providedInputProps(ownerState)
              : providedInputProps;
          const adornment =
            resolvedInputProps?.endAdornment ?? InputProps?.endAdornment;

          return {
            ...InputProps,
            ...resolvedInputProps,
            endAdornment: (
              <>
                {adornment as ReactNode}
                <InputAdornment position="end">
                  <IconButton
                    aria-label={showPassword ? "Hide password" : "Show password"}
                    onClick={() => setShowPassword((visible) => !visible)}
                    onMouseDown={(event) => event.preventDefault()}
                    edge="end"
                    size="small"
                  >
                    {showPassword ? (
                      <VisibilityOff fontSize="small" />
                    ) : (
                      <Visibility fontSize="small" />
                    )}
                  </IconButton>
                </InputAdornment>
              </>
            ),
          };
        },
      }
    : slotProps;

  const textField = (
    <TextField
      autoComplete="off"
      fullWidth
      {...props}
      id={id}
      label={labelAbove ? undefined : label}
      required={required}
      type={isPassword && showPassword ? "text" : type}
      InputProps={InputProps}
      slotProps={resolvedSlotProps}
    />
  );

  if (!labelAbove || !label) {
    return textField;
  }

  return (
    <Box
      sx={{
        display: "flex",
        flexDirection: "column",
        gap: 0.75,
        width: "100%",
        flex: "1 1 0",
        minWidth: 0,
      }}
    >
      <Typography
        component="label"
        htmlFor={id}
        sx={{ color: "text.secondary", fontSize: 13, fontWeight: 600 }}
      >
        {label}
        {required ? <Box component="span" sx={{ color: "error.light" }}> *</Box> : null}
      </Typography>
      {textField}
    </Box>
  );
}
