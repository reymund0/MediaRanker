import { useFormContext, Controller, FieldValues } from "react-hook-form";
import { BaseFileUpload, BaseFileUploadProps } from "./base-file-upload";

export interface FormFileUploadProps<TFieldValues extends FieldValues = FieldValues> extends BaseFileUploadProps {
  name: keyof TFieldValues & string;
}

export function FormFileUpload<TFieldValues extends FieldValues = FieldValues>({ name, ...props }: FormFileUploadProps<TFieldValues>) {
  const { control, setValue } = useFormContext<TFieldValues>();

  return (
    <Controller
      name={name as any}
      control={control}
      render={({ field }) => (
        <BaseFileUpload
          {...props}
          initialPreviewUrl={field.value}
          onUploadSuccess={(uploadId) => {
            // Update form state with the uploadId
            setValue(name as any, uploadId as any, { 
              shouldValidate: true, 
              shouldDirty: true, 
              shouldTouch: true 
            });
            // Call original callback if provided
            props.onUploadSuccess?.(uploadId);
          }}
        />
      )}
    />
  );
}
