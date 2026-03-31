import { ReactNode } from "react";
import {
  FieldValues,
  FormProvider,
  UseFormReturn,
  useFormState,
} from "react-hook-form";
import { BaseDialog, BaseDialogProps } from "./base-dialog";

export interface FormDialogProps<T extends FieldValues> extends Omit<
  BaseDialogProps,
  "onConfirm" | "onClose"
> {
  open: boolean;
  title: string;
  onCancel: () => void;
  onSubmit: () => void;
  methods: UseFormReturn<T>;
}

export function FormDialog<T extends FieldValues>({
  open,
  title,
  onCancel,
  onSubmit,
  methods,
  children,
  ...props
}: FormDialogProps<T> & { children: ReactNode }) {
  const { control } = methods;
  const { isSubmitting, isValid, isDirty } = useFormState({ control });
  return (
    <FormProvider {...methods}>
      <BaseDialog
        open={open}
        title={title}
        onClose={onCancel}
        onConfirm={onSubmit}
        confirmDisabled={!isValid || !isDirty}
        confirmLabel="Submit"
        confirmLoading={isSubmitting}
        {...props}
      >
        {children}
      </BaseDialog>
    </FormProvider>
  );
}
