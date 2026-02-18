"use client";

import { UserDropdown } from "@/lib/components/layout/user-dropdown";
import {
  AppBar,
  Box,
  Button,
  Toolbar,
} from "@mui/material";
import { usePathname } from "next/navigation";

type NavLink = {
  label: string;
  href: string;
};

const NAV_LINKS: NavLink[] = [
  { label: "Home", href: "/home" },
  { label: "Test", href: "/test" },
];

const NAV_BG = "#5B2A86";
const NAV_TEXT = "#F8F3FF";
const NAV_HOVER_BG = "#E3B23C";
const NAV_ACTIVE_TEXT = "#FFD166";

export function AppNavbar() {
  const pathname = usePathname();

  return (
    <AppBar
      position="static"
      elevation={0}
      sx={{
        backgroundColor: NAV_BG,
        borderBottom: "1px solid rgba(248, 243, 255, 0.18)",
      }}
    >
      <Toolbar
        sx={{
          minHeight: { xs: 64, sm: 72 },
          px: { xs: 1, sm: 2 },
          display: "grid",
          gridTemplateColumns: "48px 1fr 48px",
          alignItems: "center",
        }}
      >
        <Box />

        <Box sx={{ display: "flex", justifyContent: "center", gap: { xs: 1, sm: 2 } }}>
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
                  px: { xs: 1.5, sm: 2.5 },
                  py: { xs: 0.75, sm: 1 },
                  fontSize: { xs: "1.05rem", sm: "1.2rem" },
                  fontWeight: isActive ? 700 : 600,
                  color: isActive ? NAV_ACTIVE_TEXT : NAV_TEXT,
                  transition: "background-color 120ms ease, color 120ms ease",
                  "&:hover": {
                    backgroundColor: NAV_HOVER_BG,
                  },
                }}
              >
                {link.label}
              </Button>
            );
          })}
        </Box>

        <UserDropdown textColor={NAV_TEXT} hoverBackgroundColor={NAV_HOVER_BG} />
      </Toolbar>
    </AppBar>
  );
}
