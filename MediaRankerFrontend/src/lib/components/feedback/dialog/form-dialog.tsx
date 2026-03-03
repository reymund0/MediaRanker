import { ReactNode } from "react";
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
} from "@mui/material";
import { FieldValues, FormProvider, UseFormReturn } from "react-hook-form";

export type FormDialogProps<TFieldValues extends FieldValues = FieldValues> = {
  open: boolean;
  title: string;
  content: ReactNode;
  onCancel: () => void;
  onSubmit: () => void;
  methods: UseFormReturn<TFieldValues>;
};

export function FormDialog<TFieldValues extends FieldValues>({
  open,
  title,
  content,
  onCancel,
  onSubmit,
  methods,
}: FormDialogProps<TFieldValues>) {
  const { formState } = methods;
  const { isSubmitting, isValid } = formState;
  return (
    <FormProvider {...methods}>
      <Dialog open={open} onClose={onCancel} fullWidth maxWidth="md">
        <DialogTitle>{title}</DialogTitle>
        <DialogContent>{content}</DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button variant="outlined" onClick={onCancel} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button
            variant="contained"
            onClick={onSubmit}
            disabled={!isValid}
            loading={isSubmitting}
          >
            Submit
          </Button>
        </DialogActions>
      </Dialog>
    </FormProvider>
  );
}
