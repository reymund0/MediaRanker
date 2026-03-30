import { Controller, FieldValues, Path, useFormContext } from "react-hook-form";
import { BaseStarRating, BaseStarRankingProps } from "./base-star-rating";

type FormStarRatingProps<TForm extends FieldValues> = {
  name: Path<TForm>;
  label: string;
} & Omit<BaseStarRankingProps, "name" | "onChange" | "value">;

export function FormStarRating<TForm extends FieldValues>({
  name,
  label,
  ...rest
}: FormStarRatingProps<TForm>) {
  const { control } = useFormContext<TForm>();
  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState }) => (
        <BaseStarRating 
          {...rest} 
          {...field} 
          label={label} 
          errorMessage={fieldState.error?.message} 
        />
      )}
    />
  );
}