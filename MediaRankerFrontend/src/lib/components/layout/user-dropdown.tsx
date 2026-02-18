"use client";

import AccountCircleOutlinedIcon from "@mui/icons-material/AccountCircleOutlined";
import SettingsOutlinedIcon from "@mui/icons-material/SettingsOutlined";
import { handleSignOut } from "@/app/auth/helpers";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import {
  Box,
  IconButton,
  ListItemIcon,
  ListItemText,
  Menu,
  MenuItem,
} from "@mui/material";
import { useRouter } from "next/navigation";
import { MouseEvent, useState } from "react";

type UserDropdownProps = {
  textColor: string;
  hoverBackgroundColor: string;
};

export function UserDropdown({ textColor, hoverBackgroundColor }: UserDropdownProps) {
  const router = useRouter();
  const { showError, closeAlert } = useAlert();
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);

  const isMenuOpen = Boolean(anchorEl);

  const onOpenMenu = (event: MouseEvent<HTMLElement>) => {
    setAnchorEl(event.currentTarget);
  };

  const onCloseMenu = () => {
    setAnchorEl(null);
  };

  const onGoToSettings = () => {
    onCloseMenu();
    router.push("/settings");
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
          color: textColor,
          "&:hover": {
            backgroundColor: hoverBackgroundColor,
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
        <MenuItem onClick={onGoToSettings}>
          <ListItemIcon>
            <SettingsOutlinedIcon fontSize="small" />
          </ListItemIcon>
          <ListItemText>Settings</ListItemText>
        </MenuItem>

        <MenuItem onClick={onLogout}>
          <ListItemText inset>Logout</ListItemText>
        </MenuItem>
      </Menu>
    </Box>
  );
}
