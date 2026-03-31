import { ReactNode } from "react";
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  DialogProps,
} from "@mui/material";

export interface BaseDialogProps extends Omit<DialogProps, "onClose"> {
  title: string;
  closeDisabled?: boolean;
  closeLabel?: string;
  onClose: () => void;
  confirmDisabled?: boolean;
  confirmLabel?: string;
  confirmLoading?: boolean;
  onConfirm: () => void;
};

export function BaseDialog({
  open,
  title,
  closeDisabled,
  closeLabel,
  onClose,
  confirmDisabled,
  confirmLabel,
  confirmLoading,
  onConfirm,
  children,
  ...dialogProps
}: BaseDialogProps & { children: ReactNode }) {
  return (
    <Dialog
      open={open}
      onClose={onClose}
      fullWidth
      maxWidth="sm"
      {...dialogProps}
    >
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>{children}</DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button variant="outlined" onClick={onClose} disabled={closeDisabled}>
          {closeLabel || "Close"}
        </Button>
        <Button
          variant="contained"
          onClick={onConfirm}
          disabled={confirmDisabled}
          loading={confirmLoading}
        >
          {confirmLabel || "Confirm"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
