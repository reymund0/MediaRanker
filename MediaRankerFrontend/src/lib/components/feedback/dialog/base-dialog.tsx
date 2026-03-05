import { ReactNode } from "react";
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
} from "@mui/material";

export type BaseDialogProps = {
  open: boolean;
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
}: BaseDialogProps & { children: ReactNode }) {
  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="md">
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
