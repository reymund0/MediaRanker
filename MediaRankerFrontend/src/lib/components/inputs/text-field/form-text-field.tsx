"use client";

import { useCallback } from "react";
import {
  Controller,
  ControllerRenderProps,
  FieldValues,
  Path,
  useFormContext,
} from "react-hook-form";
import { BaseTextField, BaseTextFieldProps } from "./base-text-field";

type FormTextFieldProps<T extends FieldValues> = {
  name: Path<T>;
} & Omit<BaseTextFieldProps, "name">;

type ControlledFormTextFieldProps<T extends FieldValues> = Omit<
  BaseTextFieldProps,
  "name"
> & {
  field: ControllerRenderProps<T, Path<T>>;
  errorMessage?: string;
};

function assignRef<T>(ref: React.Ref<T> | undefined, value: T | null) {
  if (typeof ref === "function") {
    ref(value);
  } else if (ref) {
    (ref as { current: T | null }).current = value;
  }
}

function ControlledFormTextField<T extends FieldValues>({
  field,
  errorMessage,
  ...props
}: ControlledFormTextFieldProps<T>) {
  const { ref: fieldRef, ...fieldProps } = field;
  const inputRef = useCallback(
    (element: HTMLInputElement | null) => {
      fieldRef(element);
      if (props.inputRef !== fieldRef) {
        assignRef(props.inputRef, element);
      }
    },
    [fieldRef, props.inputRef],
  );

  return (
    <BaseTextField
      {...props}
      {...fieldProps}
      inputRef={inputRef}
      error={!!errorMessage}
      helperText={errorMessage ?? props.helperText}
    />
  );
}

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
        <ControlledFormTextField
          {...rest}
          field={field}
          errorMessage={fieldState.error?.message}
        />
      )}
    />
  );
}
