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
        disableGutters
        sx={{
          minHeight: 46,
          width: "100%",
          maxWidth: 1400,
          mx: "auto",
          display: "grid",
          gridTemplateColumns: "1fr auto 1fr",
          alignItems: "center",
          columnGap: 1,
        }}
      >
        <Box
          sx={{
            display: "flex",
            justifySelf: "start",
          }}
        >
          <Typography
            variant="h4"
            component="span"
            sx={{
              fontWeight: 700,
              whiteSpace: "nowrap",
            }}
          >
            Media Ranker
          </Typography>
        </Box>

        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            flexWrap: "wrap",
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
                  px: 1.25,
                  py: 0.5,
                  fontSize: "0.95rem",
                  fontWeight: isActive ? 1000 : 600,
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

        <Box sx={{ justifySelf: "end" }}>
          <UserDropdown />
        </Box>
      </Toolbar>
    </AppBar>
  );
}
