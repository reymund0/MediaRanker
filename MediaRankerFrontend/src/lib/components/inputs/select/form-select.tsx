import { Controller, FieldValues, Path, useFormContext } from "react-hook-form";
import { BaseSelect, BaseSelectOption, BaseSelectProps } from "./base-select";

type FormSelectProps<TForm extends FieldValues, TMeta = unknown> = {
  name: Path<TForm>;
  options: BaseSelectOption<TMeta>[];
  label: string;
} & Omit<BaseSelectProps<TMeta>, "name">;

export function FormSelect<TForm extends FieldValues, TMeta = unknown>({
  name,
  options,
  label,
  variant,
  fullWidth,
  ...rest
}: FormSelectProps<TForm, TMeta>) {
  const { control } = useFormContext<TForm>();
  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState }) => (
        <BaseSelect<TMeta> 
          {...rest} 
          {...field} 
          options={options} 
          label={label} 
          errorMessage={fieldState.error?.message} 
        />
      )}
    />
  );
}
