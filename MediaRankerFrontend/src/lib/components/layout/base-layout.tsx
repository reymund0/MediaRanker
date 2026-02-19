"use client";

import { AppNavbar } from "@/lib/components/layout/app-navbar";
import { Box } from "@mui/material";
import { usePathname } from "next/navigation";
import { ReactNode } from "react";

type BaseLayoutProps = {
  children: ReactNode;
};

export function BaseLayout({ children }: BaseLayoutProps) {
  const pathname = usePathname();
  const showNavbar = !pathname.startsWith("/auth");

  return (
    <Box
      sx={{
        minHeight: "100dvh",
        display: "flex",
        flexDirection: "column",
        backgroundColor: "background.default",
      }}
    >
      {showNavbar ? <AppNavbar /> : null}
      <Box sx={{ flex: 1, minHeight: 0, display: "flex", flexDirection: "column" }}>
        {children}
      </Box>
    </Box>
  );
}
