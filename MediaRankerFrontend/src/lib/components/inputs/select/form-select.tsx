import { Controller, FieldValues, Path, useFormContext } from "react-hook-form";
import { BaseSelect, BaseSelectOption, BaseSelectProps } from "./base-select";

type FormSelectProps<T extends FieldValues> = {
  name: Path<T>;
  options: BaseSelectOption[];
  label: string;
} & Omit<BaseSelectProps, "name">;

export function FormSelect<T extends FieldValues>({
  name,
  options,
  label,
  variant,
  fullWidth,
  ...rest
}: FormSelectProps<T>) {
  const { control } = useFormContext<T>();
  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState }) => (
        <BaseSelect {...rest} {...field} options={options} label={label} errorMessage={fieldState.error?.message} />
      )}
    />
  );
}
