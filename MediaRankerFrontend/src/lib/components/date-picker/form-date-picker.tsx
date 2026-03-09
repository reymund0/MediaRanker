import { ComponentProps } from "react";
import { Controller, FieldValues, Path, useFormContext } from "react-hook-form";
import { TextFieldProps } from "@mui/material";
import { BaseDatePicker } from "./base-date-picker";

type FormDatePickerProps<T extends FieldValues> = {
  name: Path<T>;
  textFieldProps?: TextFieldProps;
} & Omit<ComponentProps<typeof BaseDatePicker>, "value" | "onChange" | "textFieldProps">;

export function FormDatePicker<T extends FieldValues>({
  name,
  textFieldProps,
  ...rest
}: FormDatePickerProps<T>) {
  const { control } = useFormContext<T>();

  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState }) => (
        <BaseDatePicker
          {...rest}
          value={(field.value as Date | null) ?? null}
          onChange={(value) => field.onChange(value)}
          textFieldProps={{
            ...textFieldProps,
            error: !!fieldState.error,
            helperText: fieldState.error?.message ?? textFieldProps?.helperText,
          }}
        />
      )}
    />
  );
}
