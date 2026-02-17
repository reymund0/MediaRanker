"use client";

import { Alert, AlertColor, AlertProps } from "@mui/material";
import { ReactNode, useEffect } from "react";

const DEFAULT_AUTO_HIDE_DURATION_MS: Record<AlertColor, number> = {
  success: 3000,
  info: 5000,
  warning: 5000,
  error: 7000,
};

export type BaseAlertProps = Omit<
  AlertProps,
  "severity" | "onClose" | "children"
> & {
  open?: boolean;
  severity: AlertColor;
  children: ReactNode;
  onClose?: () => void;
  persist?: boolean;
  autoHideDurationMs?: number;
};

export function BaseAlert({
  open = true,
  severity,
  children,
  onClose,
  persist = false,
  autoHideDurationMs,
  ...alertProps
}: BaseAlertProps) {
  useEffect(() => {
    if (!open || persist || !onClose) {
      return;
    }

    const timeoutDuration =
      autoHideDurationMs ?? DEFAULT_AUTO_HIDE_DURATION_MS[severity];
    const timeoutId = window.setTimeout(() => {
      onClose();
    }, timeoutDuration);

    return () => {
      window.clearTimeout(timeoutId);
    };
  }, [autoHideDurationMs, onClose, open, persist, severity]);

  if (!open) {
    return null;
  }

  return (
    <Alert severity={severity} onClose={onClose} {...alertProps}>
      {children}
    </Alert>
  );
}
