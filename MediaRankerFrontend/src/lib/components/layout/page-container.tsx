import { Box, BoxProps } from "@mui/material";
import { SxProps, Theme } from "@mui/material/styles";
import { ReactNode } from "react";

export interface PageContainerProps extends BoxProps {
  children: ReactNode;
}

/** Shared content width and desktop gutters for non-authenticated pages. */
export function PageContainer({ children, sx, ...props }: PageContainerProps) {
  const containerSx: SxProps<Theme> = [
    {
      boxSizing: "border-box",
      width: "100%",
      maxWidth: 1280,
      px: 5,
      py: 6,
      mx: "auto",
    },
    ...(Array.isArray(sx) ? sx : sx ? [sx] : []),
  ];

  return (
    <Box {...props} sx={containerSx}>
      {children}
    </Box>
  );
}
