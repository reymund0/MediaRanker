import { ReactNode } from "react";
import { FieldValues, FormProvider, UseFormReturn } from "react-hook-form";
import { BaseDialog } from "./base-dialog";

export type FormDialogProps<T extends FieldValues> = {
  open: boolean;
  title: string;
  onCancel: () => void;
  onSubmit: () => void;
  methods: UseFormReturn<T>;
};

export function FormDialog<T extends FieldValues>({
  open,
  title,
  onCancel,
  onSubmit,
  methods,
  children,
}: FormDialogProps<T> & { children: ReactNode }) {
  const { formState } = methods;
  const { isSubmitting, isValid, isDirty } = formState;
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
      >
        {children}
      </BaseDialog>
    </FormProvider>
  );
}
