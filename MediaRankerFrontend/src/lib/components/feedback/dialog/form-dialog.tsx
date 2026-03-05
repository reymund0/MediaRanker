import { ReactNode } from "react";
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
} from "@mui/material";
import { FieldValues, FormProvider, UseFormReturn } from "react-hook-form";

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
      <Dialog open={open} onClose={onCancel} fullWidth maxWidth="md">
        <DialogTitle>{title}</DialogTitle>
        <DialogContent>{children}</DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button variant="outlined" onClick={onCancel} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button
            variant="contained"
            onClick={onSubmit}
            disabled={!isValid || !isDirty}
            loading={isSubmitting}
          >
            Submit
          </Button>
        </DialogActions>
      </Dialog>
    </FormProvider>
  );
}
