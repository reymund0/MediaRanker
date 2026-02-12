import {
  Controller,
  FieldValues,
  Path,
  useFormContext,
} from "react-hook-form";
import { TextFieldProps } from "@mui/material";
import { BaseTextField } from "./base-text-field";

type FormTextFieldProps<T extends FieldValues> = {
  name: Path<T>;
} & Omit<TextFieldProps, "name">;

export function FormTextField<T extends FieldValues>({
  name,
  ...rest
}: FormTextFieldProps<T>) {
  const { control } = useFormContext<T>();
  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState }) => (
        <BaseTextField
          {...rest}
          {...field}
          error={!!fieldState.error}
          helperText={fieldState.error?.message ?? rest.helperText}
        />
      )}
    />
  );
}
