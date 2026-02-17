"use client";

import { AlertColor } from "@mui/material";
import {
  createContext,
  ReactNode,
  useCallback,
  useContext,
  useMemo,
  useState,
} from "react";
import { BaseAlert, BaseAlertProps } from "./base-alert";

export type AlertDisplayOptions = {
  persist?: boolean;
  autoHideDurationMs?: number;
};

export type ShowAlertOptions = AlertDisplayOptions & {
  message: string;
  severity?: AlertColor;
};

export type AlertContextValue = {
  showSuccess: (message: string, options?: AlertDisplayOptions) => void;
  showInfo: (message: string, options?: AlertDisplayOptions) => void;
  showWarning: (message: string, options?: AlertDisplayOptions) => void;
  showError: (message: string, options?: AlertDisplayOptions) => void;
  closeAlert: () => void;
};

export const AlertContext = createContext<AlertContextValue>({
  showSuccess: () => {
    throw new Error("Function not implemented.");
  },
  showInfo: () => {
    throw new Error("Function not implemented.");
  },
  showWarning: () => {
    throw new Error("Function not implemented.");
  },
  showError: () => {
    throw new Error("Function not implemented.");
  },
  closeAlert: () => {
    throw new Error("Function not implemented.");
  },
});

export const useAlert = () => useContext(AlertContext);

type AlertState = {
  message: string;
  severity: AlertColor;
  persist: boolean;
  autoHideDurationMs?: number;
};

export type AlertProviderProps = {
  children: ReactNode;
  defaultSeverity?: AlertColor;
};

export function AlertProvider({
  children,
  defaultSeverity = "info",
}: AlertProviderProps) {
  const [alertState, setAlertState] = useState<AlertState | null>(null);

  const closeAlert = useCallback(() => {
    setAlertState(null);
  }, []);

  const showAlert = useCallback(
    ({
      message,
      severity = defaultSeverity,
      persist = false,
      autoHideDurationMs,
    }: ShowAlertOptions) => {
      setAlertState({ message, severity, persist, autoHideDurationMs });
    },
    [defaultSeverity],
  );

  const showSuccess = useCallback(
    (message: string, options?: AlertDisplayOptions) => {
      showAlert({ message, severity: "success", ...options });
    },
    [showAlert],
  );

  const showInfo = useCallback(
    (message: string, options?: AlertDisplayOptions) => {
      showAlert({ message, severity: "info", ...options });
    },
    [showAlert],
  );

  const showWarning = useCallback(
    (message: string, options?: AlertDisplayOptions) => {
      showAlert({ message, severity: "warning", ...options });
    },
    [showAlert],
  );

  const showError = useCallback(
    (message: string, options?: AlertDisplayOptions) => {
      showAlert({ message, severity: "error", ...options });
    },
    [showAlert],
  );

  const baseAlertProps = useMemo<Omit<BaseAlertProps, "children">>(
    () => ({
      open: alertState !== null,
      severity: alertState?.severity ?? defaultSeverity,
      onClose: closeAlert,
      persist: alertState?.persist ?? false,
      autoHideDurationMs: alertState?.autoHideDurationMs,
    }),
    [alertState, closeAlert, defaultSeverity],
  );

  const alertMessage = alertState?.message ?? "";

  const contextValue = useMemo<AlertContextValue>(
    () => ({
      showSuccess,
      showInfo,
      showWarning,
      showError,
      closeAlert,
    }),
    [closeAlert, showError, showInfo, showSuccess, showWarning],
  );

  return (
    <AlertContext.Provider value={contextValue}>
      {children}
      <BaseAlert
        {...baseAlertProps}
        sx={{
          position: "fixed",
          top: 16,
          left: "50%",
          transform: "translateX(-50%)",
          zIndex: 1400,
          minWidth: { xs: "calc(100% - 32px)", sm: 420 },
          maxWidth: "min(640px, calc(100% - 32px))",
        }}
      >
        {alertMessage}
      </BaseAlert>
    </AlertContext.Provider>
  );
}
