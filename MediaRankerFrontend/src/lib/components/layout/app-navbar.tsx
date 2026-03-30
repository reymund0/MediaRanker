"use client";

import { UserDropdown } from "@/lib/components/layout/user-dropdown";
import { AppBar, Box, Button, Toolbar, Typography } from "@mui/material";
import { usePathname } from "next/navigation";

type NavLink = {
  label: string;
  href: string;
};

const NAV_LINKS: NavLink[] = [
  { label: "Media", href: "/media" },
  { label: "Templates", href: "/templates" },
  { label: "Reviews", href: "/reviews" },
];

export function AppNavbar() {
  const pathname = usePathname();

  return (
    <AppBar position="static" elevation={0}>
      <Toolbar
        sx={{
          minHeight: 52,
          px: 2,
          display: "grid",
          gridTemplateColumns: "auto 1fr 48px",
          alignItems: "center",
          columnGap: 1,
        }}
      >
        <Typography
          variant="h6"
          component="span"
          sx={{
            fontWeight: 700,
            color: "primary.contrastText",
            whiteSpace: "nowrap",
          }}
        >
          Media Ranker
        </Typography>

        <Box
          sx={{
            display: "flex",
            justifyContent: "center",
            gap: 2,
          }}
        >
          {NAV_LINKS.map((link) => {
            const isActive = pathname === link.href;

            return (
              <Button
                key={link.href}
                href={link.href}
                color="inherit"
                sx={{
                  textTransform: "none",
                  borderRadius: 1.5,
                  px: 2.5,
                  py: 1,
                  fontSize: "1.2rem",
                  fontWeight: isActive ? 700 : 600,
                  color: isActive ? "secondary.light" : "primary.contrastText",
                  transition: "background-color 120ms ease, color 120ms ease",
                  "&:hover": {
                    backgroundColor: isActive
                      ? "action.selected"
                      : "action.hover",
                  },
                }}
              >
                {link.label}
              </Button>
            );
          })}
        </Box>

        <UserDropdown />
      </Toolbar>
    </AppBar>
  );
}
