"use client";

import AddIcon from "@mui/icons-material/Add";
import SearchIcon from "@mui/icons-material/Search";
import { AppBar, Box, Button, Toolbar } from "@mui/material";
import { usePathname, useRouter } from "next/navigation";
import { useCallback, useEffect } from "react";
import { useReviewExperience } from "@/app/reviews/_components/review-experience";
import { AppWordmark } from "@/lib/components/layout/app-wordmark";
import { PageContainer } from "@/lib/components/layout/page-container";
import { UserDropdown } from "@/lib/components/layout/user-dropdown";

const NAV_LINKS = [
  { label: "Library", href: "/reviews" },
  { label: "Catalog", href: "/media" },
  { label: "Templates", href: "/templates" },
];

export function AppNavbar() {
  const pathname = usePathname();
  const router = useRouter();
  const { openNewReview } = useReviewExperience();
  const catalogIsOpen = pathname === "/media" || pathname.startsWith("/media/");

  const focusCatalogSearch = useCallback(() => {
    if (catalogIsOpen) {
      document.getElementById("catalog-search")?.focus();
      return;
    }

    router.push("/media?focus=1");
  }, [catalogIsOpen, router]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (
        event.key.toLowerCase() !== "k" ||
        (!event.ctrlKey && !event.metaKey) ||
        event.altKey
      ) {
        return;
      }

      event.preventDefault();
      focusCatalogSearch();
    };

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [focusCatalogSearch]);

  return (
    <AppBar position="static" elevation={0}>
      <Toolbar
        disableGutters
        sx={{
          minHeight: "68px !important",
          width: "100%",
        }}
      >
        <PageContainer
          sx={{
            display: "flex",
            alignItems: "center",
            minHeight: "68px",
            py: 0,
            gap: 3,
          }}
        >
          <AppWordmark />

          <Box
            component="nav"
            aria-label="Primary navigation"
            sx={{ display: "flex", alignItems: "center", gap: 0.5 }}
          >
            {NAV_LINKS.map(({ label, href }) => {
              const active = pathname === href || pathname.startsWith(`${href}/`);

              return (
                <Button
                  key={href}
                  href={href}
                  aria-current={active ? "page" : undefined}
                  color="inherit"
                  sx={{
                    minHeight: 36,
                    px: 1.5,
                    color: active ? "text.primary" : "text.muted",
                    backgroundColor: active ? "background.raised" : "transparent",
                    "&:hover": {
                      color: "text.primary",
                      backgroundColor: "background.raised",
                    },
                  }}
                >
                  {label}
                </Button>
              );
            })}
          </Box>

          <Box sx={{ flex: 1 }} />

          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
            {!catalogIsOpen && (
              <Button
                variant="outlined"
                onClick={focusCatalogSearch}
                startIcon={<SearchIcon />}
                aria-label="Search the catalog, Ctrl K"
                sx={{
                  minWidth: 260,
                  justifyContent: "flex-start",
                  color: "text.muted",
                  borderColor: "divider",
                  px: 1.5,
                  "&:hover": {
                    color: "text.primary",
                    borderColor: "text.muted",
                  },
                }}
              >
                <Box sx={{ flex: 1, textAlign: "left" }}>Search the catalog</Box>
                <Box
                  component="kbd"
                  sx={{
                    px: 0.75,
                    py: 0.25,
                    ml: 1,
                    border: "1px solid",
                    borderColor: "divider",
                    borderRadius: 1,
                    fontFamily: "var(--font-mono), monospace",
                    fontSize: 11,
                    lineHeight: 1.3,
                    color: "text.secondary",
                  }}
                >
                  Ctrl K
                </Box>
              </Button>
            )}
            <Button
              variant="contained"
              onClick={() => openNewReview()}
              startIcon={<AddIcon />}
            >
              New review
            </Button>
          </Box>

          <UserDropdown />
        </PageContainer>
      </Toolbar>
    </AppBar>
  );
}
