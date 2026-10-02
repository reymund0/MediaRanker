"use client";

import AccountCircleOutlinedIcon from "@mui/icons-material/AccountCircleOutlined";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import Logout from "@mui/icons-material/Logout";
import SourceOutlinedIcon from "@mui/icons-material/SourceOutlined";
import {
  Box,
  Divider,
  IconButton,
  ListItemIcon,
  ListItemText,
  Menu,
  MenuItem,
  Typography,
} from "@mui/material";
import { MouseEvent, useState } from "react";
import { useRouter } from "next/navigation";
import { handleSignOut } from "@/app/auth/helpers";
import { useUser } from "@/lib/auth/user-provider";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";

export function UserDropdown() {
  const router = useRouter();
  const { authResolved, isAuthenticated, isLocalTestUser, username } =
    useUser();
  const { showError, closeAlert } = useAlert();
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
  const isMenuOpen = Boolean(anchorEl);
  const displayName = username?.trim() || "Account";
  const initials = displayName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => word.charAt(0))
    .join("")
    .toUpperCase();

  const onOpenMenu = (event: MouseEvent<HTMLElement>) => {
    setAnchorEl(event.currentTarget);
  };

  const onCloseMenu = () => {
    setAnchorEl(null);
  };

  const onLogout = async () => {
    onCloseMenu();
    closeAlert();

    const result = await handleSignOut();
    if (result.success) {
      router.push("/auth/login");
      return;
    }

    showError(result.error || "Failed to sign out");
  };

  return (
    <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
      <IconButton
        onClick={onOpenMenu}
        aria-label="Open account menu"
        aria-controls={isMenuOpen ? "user-menu" : undefined}
        aria-expanded={isMenuOpen ? "true" : undefined}
        aria-haspopup="menu"
        sx={{
          width: 40,
          height: 40,
          border: "1px solid",
          borderColor: "divider",
          backgroundColor: "background.raised",
          color: "text.primary",
          fontSize: 12,
          fontWeight: 700,
          "&:hover": {
            backgroundColor: "action.hover",
          },
        }}
      >
        {initials || <AccountCircleOutlinedIcon fontSize="small" />}
      </IconButton>

      <Menu
        id="user-menu"
        anchorEl={anchorEl}
        open={isMenuOpen}
        onClose={onCloseMenu}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
        transformOrigin={{ vertical: "top", horizontal: "right" }}
        slotProps={{ paper: { sx: { minWidth: 228, p: 0.75 } } }}
      >
        <Box
          sx={{
            display: "flex",
            flexDirection: "column",
            gap: 0.25,
            px: 1.5,
            py: 1,
          }}
        >
          <Typography variant="caption" color="text.muted">
            Signed in as
          </Typography>
          <Typography variant="body2" fontWeight={600} color="text.primary">
            {displayName}
          </Typography>
        </Box>
        <Divider sx={{ my: 0.75 }} />
        {authResolved && isAuthenticated && !isLocalTestUser && (
          <MenuItem
            onClick={() => {
              onCloseMenu();
              router.push("/account/change-password");
            }}
          >
            <ListItemIcon sx={{ minWidth: 36 }}>
              <LockOutlinedIcon fontSize="small" />
            </ListItemIcon>
            <ListItemText>Change password</ListItemText>
          </MenuItem>
        )}
        <MenuItem
          onClick={() => {
            onCloseMenu();
            router.push("/credits");
          }}
        >
          <ListItemIcon sx={{ minWidth: 36 }}>
            <SourceOutlinedIcon fontSize="small" />
          </ListItemIcon>
          <ListItemText>Credits &amp; data sources</ListItemText>
        </MenuItem>
        <MenuItem onClick={onLogout}>
          <ListItemIcon sx={{ minWidth: 36 }}>
            <Logout fontSize="small" />
          </ListItemIcon>
          <ListItemText>Sign out</ListItemText>
        </MenuItem>
      </Menu>
    </Box>
  );
}
