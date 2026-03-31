// theme.ts
import { alpha, createTheme } from "@mui/material/styles";
import { LinkBehavior } from "@/lib/components/navigation/linkBehavior";

const palette = {
  primary: {
    main: "#7C3AED",
    light: "#A78BFA",
    dark: "#5B21B6",
    contrastText: "#F8F5FF",
  },
  secondary: {
    main: "#14B8A6",
    light: "#5EEAD4",
    dark: "#0F766E",
    contrastText: "#042F2E",
  },
  background: {
    default: "#0F1115",
    paper: "#171A21",
  },
  text: {
    primary: "#F3F4F6",
    secondary: "#9CA3AF",
  },
  divider: "#2A2F3A",
  action: {
    hover: "rgba(20, 184, 166, 0.12)",
    selected: "rgba(20, 184, 166, 0.24)",
  },
} as const;

const theme = createTheme({
  palette: {
    mode: "dark",
    primary: palette.primary,
    secondary: palette.secondary,
    background: palette.background,
    text: palette.text,
    divider: palette.divider,
    action: palette.action,
  },
  components: {
    MuiLink: {
      defaultProps: {
        component: LinkBehavior,
      },
    },
    MuiButtonBase: {
      defaultProps: {
        LinkComponent: LinkBehavior,
      },
    },
    MuiCssBaseline: {
      styleOverrides: {
        body: {
          backgroundColor: palette.background.default,
          color: palette.text.primary,
        },
      },
    },
    MuiAppBar: {
      styleOverrides: {
        root: {
          backgroundColor: palette.primary.main,
          color: palette.primary.contrastText,
          borderBottom: `1px solid ${alpha(palette.primary.contrastText, 0.18)}`,
        },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: {
          backgroundImage: "none",
        },
      },
    },
    MuiCard: {
      styleOverrides: {
        root: {
          backgroundColor: palette.background.paper,
          border: `1px solid ${palette.divider}`,
        },
      },
    },
  },
});

export default theme;
