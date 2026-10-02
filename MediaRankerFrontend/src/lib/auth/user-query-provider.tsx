"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useEffect, useState, type ReactNode } from "react";

/** Own both cached data and mounted UI state for one authenticated identity. */
export function UserQueryProvider({
  userId,
  children,
}: {
  userId: string | undefined;
  children: ReactNode;
}) {
  return <QueryScope key={JSON.stringify(userId ?? null)}>{children}</QueryScope>;
}

function QueryScope({ children }: { children: ReactNode }) {
  const [client] = useState(() => new QueryClient());

  useEffect(() => () => client.clear(), [client]);

  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}
