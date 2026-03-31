"use client";

import AccountCircleOutlinedIcon from "@mui/icons-material/AccountCircleOutlined";
import Logout from "@mui/icons-material/Logout"
import { handleSignOut } from "@/app/auth/helpers";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import {
  Box,
  Divider,
  IconButton,
  ListItemIcon,
  ListItemText,
  Menu,
  MenuItem,
} from "@mui/material";
import { useRouter } from "next/navigation";
import { MouseEvent, useState } from "react";
import { useUser } from "@/lib/auth/user-provider";

export function UserDropdown() {
  const router = useRouter();
  const { username } = useUser();
  const { showError, closeAlert } = useAlert();
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);

  const isMenuOpen = Boolean(anchorEl);

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
        color="inherit"
        onClick={onOpenMenu}
        aria-label="Open user menu"
        aria-controls={isMenuOpen ? "user-menu" : undefined}
        aria-expanded={isMenuOpen ? "true" : undefined}
        aria-haspopup="menu"
        sx={{
          color: "primary.contrastText",
          "&:hover": {
            backgroundColor: "action.hover",
          },
        }}
      >
        <AccountCircleOutlinedIcon fontSize="medium" />
      </IconButton>

      <Menu
        id="user-menu"
        anchorEl={anchorEl}
        open={isMenuOpen}
        onClose={onCloseMenu}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
        transformOrigin={{ vertical: "top", horizontal: "right" }}
      >
        <ListItemText sx={{ display: "flex", justifyContent: "center" }}>
          Hi, {username}!
        </ListItemText>
        <Divider />
        <MenuItem onClick={onLogout}>
          <ListItemIcon sx={{ minWidth: 36 }}>
            <Logout fontSize="small" />
          </ListItemIcon>
          <ListItemText>Logout</ListItemText>
        </MenuItem>
      </Menu>
    </Box>
  );
}
