import { Controller, FieldValues, Path, useFormContext } from "react-hook-form";
import { BaseScoreInput, BaseScoreInputProps } from "./base-score-input";

export type FormScoreInputProps<TForm extends FieldValues> = {
  name: Path<TForm>;
} & Omit<BaseScoreInputProps, "value" | "onChange">;

export function FormScoreInput<TForm extends FieldValues>({
  name,
  ...props
}: FormScoreInputProps<TForm>) {
  const { control } = useFormContext<TForm>();

  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState }) => (
        <BaseScoreInput
          {...props}
          value={typeof field.value === "number" ? field.value : null}
          onChange={field.onChange}
          errorMessage={fieldState.error?.message ?? props.errorMessage}
        />
      )}
    />
  );
}
