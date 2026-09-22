import { useEffect, useRef, useState } from "react";

const POLL_INTERVAL_MS = 2_000;
const POLL_WINDOW_MS = 30_000;

type PendingCoverRefreshOptions = {
  viewKey: string;
  hasPendingCovers: boolean;
  refetch: () => Promise<unknown>;
  enabled?: boolean;
};

/** Refetches an active view briefly while its currently displayed covers resolve. */
export function usePendingCoverRefresh({
  viewKey,
  hasPendingCovers,
  refetch,
  enabled = true,
}: PendingCoverRefreshOptions) {
  const [isDocumentVisible, setIsDocumentVisible] = useState(true);
  const pollingWindow = useRef<{ key: string; startedAt: number } | null>(null);

  useEffect(() => {
    const handleVisibilityChange = () => {
      const isVisible = document.visibilityState === "visible";
      setIsDocumentVisible(isVisible);
      if (isVisible) {
        pollingWindow.current = null;
      }
    };

    handleVisibilityChange();
    document.addEventListener("visibilitychange", handleVisibilityChange);
    return () =>
      document.removeEventListener("visibilitychange", handleVisibilityChange);
  }, []);

  useEffect(() => {
    if (!enabled || !hasPendingCovers || !isDocumentVisible) {
      return;
    }

    if (pollingWindow.current?.key !== viewKey) {
      pollingWindow.current = { key: viewKey, startedAt: Date.now() };
    }
    const remainingMs =
      POLL_WINDOW_MS - (Date.now() - pollingWindow.current.startedAt);
    if (remainingMs <= 0) {
      return;
    }

    let inFlight = false;
    const intervalId = window.setInterval(() => {
      if (!inFlight) {
        inFlight = true;
        void refetch().finally(() => {
          inFlight = false;
        });
      }
    }, POLL_INTERVAL_MS);
    const timeoutId = window.setTimeout(
      () => window.clearInterval(intervalId),
      remainingMs,
    );
    return () => {
      window.clearInterval(intervalId);
      window.clearTimeout(timeoutId);
    };
  }, [enabled, hasPendingCovers, isDocumentVisible, refetch, viewKey]);
}
