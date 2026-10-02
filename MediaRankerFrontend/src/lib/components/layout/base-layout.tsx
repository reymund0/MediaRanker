"use client";

import { Box, Link, Typography } from "@mui/material";
import { ReactNode } from "react";
import { usePathname } from "next/navigation";
import { ReviewExperienceProvider } from "@/app/reviews/_components/review-experience";
import { AppNavbar } from "@/lib/components/layout/app-navbar";
import { PageContainer } from "@/lib/components/layout/page-container";

type BaseLayoutProps = {
  children: ReactNode;
};

function AttributionFooter() {
  return (
    <PageContainer
      component="footer"
      sx={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        gap: 2,
        py: 2.5,
        mt: 4,
        borderTop: "1px solid",
        borderColor: "divider",
      }}
    >
      <Typography variant="caption" color="text.muted">
        This product uses the{" "}
        <Box
          component="a"
          href="https://www.themoviedb.org/"
          target="_blank"
          rel="noreferrer"
          sx={{ color: "text.secondary", textDecoration: "underline" }}
        >
          TMDB API
        </Box>
        {" "}but is not endorsed or certified by TMDB. {" "}
        <Box
          component="a"
          href="https://www.igdb.com/"
          target="_blank"
          rel="noreferrer"
          sx={{ color: "text.secondary", textDecoration: "underline" }}
        >
          IGDB
        </Box>
        {" "}provides video game data.
      </Typography>
      <Link href="/credits" underline="hover" variant="caption" sx={{ flexShrink: 0 }}>
        Credits &amp; data sources
      </Link>
    </PageContainer>
  );
}

export function BaseLayout({ children }: BaseLayoutProps) {
  const pathname = usePathname();
  const showShell = pathname !== "/auth" && !pathname.startsWith("/auth/");

  return (
    <ReviewExperienceProvider>
      <Box
        sx={{
          minHeight: "100dvh",
          display: "flex",
          flexDirection: "column",
          backgroundColor: "background.default",
        }}
      >
        {showShell ? <AppNavbar /> : null}
        <Box
          component="main"
          sx={{
            flex: 1,
            minHeight: 0,
            display: "flex",
            flexDirection: "column",
          }}
        >
          {children}
        </Box>
        {showShell ? <AttributionFooter /> : null}
      </Box>
    </ReviewExperienceProvider>
  );
}
