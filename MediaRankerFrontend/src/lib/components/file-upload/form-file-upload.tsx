import {
  useFormContext,
  Controller,
  FieldValues,
  Path,
  PathValue,
} from "react-hook-form";
import { BaseFileUpload, BaseFileUploadProps } from "./base-file-upload";

export interface FormFileUploadProps<
  TFieldValues extends FieldValues = FieldValues,
  TName extends Path<TFieldValues> = Path<TFieldValues>,
> extends BaseFileUploadProps {
  name: TName;
}

export function FormFileUpload<
  TFieldValues extends FieldValues = FieldValues,
  TName extends Path<TFieldValues> = Path<TFieldValues>,
>({
  name,
  initialPreviewUrl,
  ...props
}: FormFileUploadProps<TFieldValues, TName>) {
  const { control, setValue } = useFormContext<TFieldValues>();

  return (
    <Controller
      name={name}
      control={control}
      render={({ field }) => (
        <BaseFileUpload
          {...props}
          initialPreviewUrl={
            initialPreviewUrl ??
            (typeof field.value === "string" ? field.value : undefined)
          }
          onUploadSuccess={(uploadId) => {
            // Update form state with the uploadId
            setValue(name, uploadId as PathValue<TFieldValues, TName>, {
              shouldValidate: true,
              shouldDirty: true,
              shouldTouch: true,
            });
            // Call original callback if provided
            props.onUploadSuccess?.(uploadId);
          }}
        />
      )}
    />
  );
}
