import { Box, Collapse, SxProps, Theme } from "@mui/material";
import { ReactNode } from "react";

type BaseDetailPanelProps = {
  open: boolean;
  children: ReactNode;
  sx?: SxProps<Theme>;
};

export function BaseDetailPanel({ open, children, sx }: BaseDetailPanelProps) {
  return (
    <Collapse in={open} timeout="auto" unmountOnExit>
      <Box
        sx={{
          px: { xs: 2, md: 3 },
          py: 2,
          borderTop: "1px solid",
          borderColor: "divider",
          backgroundColor: "background.default",
          ...sx,
        }}
      >
        {children}
      </Box>
    </Collapse>
  );
}
