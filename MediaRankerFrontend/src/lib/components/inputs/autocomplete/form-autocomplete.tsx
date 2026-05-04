import { Controller, FieldValues, Path, useFormContext } from "react-hook-form";
import { BaseAutocomplete, BaseAutocompleteProps } from "./base-autocomplete";
import { BaseSelectOption } from "../select/base-select";

type FormAutocompleteProps<TForm extends FieldValues, TMeta = unknown> = {
  name: Path<TForm>;
} & Omit<BaseAutocompleteProps<TMeta>, "value" | "onSelectOption" | "name">;

export function FormAutocomplete<TForm extends FieldValues, TMeta = unknown>({
  name,
  ...rest
}: FormAutocompleteProps<TForm, TMeta>) {
  const { control } = useFormContext<TForm>();
  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState }) => (
        <BaseAutocomplete<TMeta>
          {...rest}
          value={(field.value as BaseSelectOption<TMeta> | null) ?? null}
          onSelectOption={(opt) => field.onChange(opt)}
          onBlur={field.onBlur}
          inputRef={field.ref}
          name={field.name}
          error={!!fieldState.error}
          helperText={fieldState.error?.message}
        />
      )}
    />
  );
}
