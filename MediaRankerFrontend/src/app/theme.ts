import { alpha, createTheme } from "@mui/material/styles";
import type { CSSProperties } from "react";
import { LinkBehavior } from "@/lib/components/navigation/linkBehavior";

declare module "@mui/material/styles" {
  interface TypeBackground {
    raised: string;
  }

  interface TypeText {
    muted: string;
    placeholder: string;
  }

  interface TypographyVariants {
    numeric: CSSProperties;
  }

  interface TypographyVariantsOptions {
    numeric?: CSSProperties;
  }
}

declare module "@mui/material/Typography" {
  interface TypographyPropsVariantOverrides {
    numeric: true;
  }
}

const colors = {
  ground: "#0E0D12",
  surface: "#16151C",
  raised: "#1F1D27",
  raisedHover: "#2A2833",
  rowDivider: "#23212B",
  line: "#2C2A36",
  lineStrong: "#3B3847",
  lineHover: "#5A566A",
  text: "#F2F0F7",
  textSecondary: "#D3CFDE",
  textMuted: "#A29EB1",
  textPlaceholder: "#7D798C",
  accent: "#8B5CF6",
  accentHover: "#9D74F7",
  accentText: "#A78BFA",
  accentTextHover: "#C4B5FD",
  accentTint: "#231A3D",
  accentBorder: "#4B3585",
  danger: "#C93B40",
  dangerText: "#F06B6F",
  dangerInput: "#E5484D",
  success: "#5FD08F",
} as const;

const theme = createTheme({
  palette: {
    mode: "dark",
    primary: {
      main: colors.accent,
      light: colors.accentHover,
      dark: "#6D42C2",
      contrastText: "#FFFFFF",
    },
    secondary: {
      main: colors.accentText,
      light: colors.accentTextHover,
      dark: colors.accent,
      contrastText: colors.ground,
    },
    error: {
      main: colors.danger,
      light: colors.dangerText,
      dark: "#9E292F",
      contrastText: "#FFFFFF",
    },
    success: {
      main: colors.success,
      light: "#8AE3AE",
      dark: "#36A969",
      contrastText: colors.ground,
    },
    background: {
      default: colors.ground,
      paper: colors.surface,
      raised: colors.raised,
    },
    text: {
      primary: colors.text,
      secondary: colors.textSecondary,
      muted: colors.textMuted,
      placeholder: colors.textPlaceholder,
      disabled: colors.textPlaceholder,
    },
    divider: colors.line,
    action: {
      hover: colors.raisedHover,
      selected: colors.accentTint,
      disabled: colors.textPlaceholder,
      disabledBackground: colors.raised,
      focus: alpha(colors.accent, 0.24),
    },
  },
  shape: {
    borderRadius: 10,
  },
  typography: {
    fontFamily: "var(--font-body), system-ui, sans-serif",
    h1: {
      fontFamily: "var(--font-display), sans-serif",
      fontSize: "3.5rem",
      fontWeight: 800,
      lineHeight: 1,
      letterSpacing: "-0.035em",
    },
    h2: {
      fontFamily: "var(--font-display), sans-serif",
      fontSize: "1.625rem",
      fontWeight: 700,
      lineHeight: 1.15,
      letterSpacing: "-0.02em",
    },
    h3: {
      fontFamily: "var(--font-display), sans-serif",
      fontSize: "1.375rem",
      fontWeight: 700,
      lineHeight: 1.2,
      letterSpacing: "-0.015em",
    },
    h4: {
      fontFamily: "var(--font-display), sans-serif",
      fontWeight: 700,
      letterSpacing: "-0.015em",
    },
    h5: {
      fontFamily: "var(--font-display), sans-serif",
      fontWeight: 700,
      letterSpacing: "-0.01em",
    },
    h6: {
      fontFamily: "var(--font-display), sans-serif",
      fontWeight: 700,
    },
    body1: {
      fontSize: "0.9375rem",
      lineHeight: 1.55,
    },
    body2: {
      fontSize: "0.8125rem",
      lineHeight: 1.5,
    },
    button: {
      fontSize: "0.9375rem",
      fontWeight: 600,
      lineHeight: 1.2,
      textTransform: "none",
    },
    caption: {
      fontSize: "0.8125rem",
      lineHeight: 1.45,
    },
    numeric: {
      fontFamily: "var(--font-mono), monospace",
      fontSize: "0.875rem",
      fontWeight: 600,
      fontVariantNumeric: "tabular-nums",
      color: colors.accentText,
    },
  },
  components: {
    MuiLink: {
      defaultProps: {
        component: LinkBehavior,
      },
      styleOverrides: {
        root: {
          color: colors.accentText,
          textUnderlineOffset: "0.18em",
          "&:hover": {
            color: colors.accentTextHover,
          },
        },
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
          backgroundColor: colors.ground,
          color: colors.text,
        },
        "::selection": {
          backgroundColor: alpha(colors.accent, 0.38),
        },
        ".MuiDataGrid-root": {
          color: colors.text,
          borderColor: colors.line,
          "--DataGrid-containerBackground": colors.surface,
          "& .MuiDataGrid-columnHeaders, & .MuiDataGrid-footerContainer": {
            borderColor: colors.line,
          },
          "& .MuiDataGrid-row:hover": {
            backgroundColor: colors.raisedHover,
          },
        },
      },
    },
    MuiAppBar: {
      styleOverrides: {
        root: {
          backgroundColor: colors.ground,
          color: colors.text,
          borderBottom: `1px solid ${colors.line}`,
          backgroundImage: "none",
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
          backgroundColor: colors.surface,
          border: `1px solid ${colors.line}`,
          borderRadius: 16,
          boxShadow: "none",
        },
      },
    },
    MuiButton: {
      styleOverrides: {
        root: {
          minHeight: 40,
          borderRadius: 10,
          paddingInline: 16,
          textTransform: "none",
          fontWeight: 600,
          "&.MuiButton-textPrimary": {
            color: colors.text,
          },
          "&.MuiButton-outlinedPrimary": {
            color: colors.text,
            borderColor: colors.lineStrong,
          },
          "&.MuiButton-textError": {
            color: colors.dangerText,
          },
          "&.MuiButton-outlinedError": {
            color: colors.dangerText,
            borderColor: colors.danger,
          },
          "&.Mui-focusVisible": {
            boxShadow: `0 0 0 3px ${alpha(colors.accent, 0.24)}`,
          },
        },
        contained: {
          boxShadow: "none",
          "&:hover": {
            boxShadow: "none",
            backgroundColor: colors.accentHover,
          },
        },
        containedError: {
          "&:hover": {
            backgroundColor: "#A92F34",
          },
        },
        outlined: {
          borderColor: colors.lineStrong,
          "&:hover": {
            borderColor: colors.lineHover,
            backgroundColor: colors.raisedHover,
          },
        },
        text: {
          "&:hover": {
            backgroundColor: colors.raisedHover,
          },
        },
      },
    },
    MuiOutlinedInput: {
      styleOverrides: {
        root: {
          borderRadius: 10,
          backgroundColor: colors.ground,
          "& .MuiOutlinedInput-notchedOutline": {
            borderColor: colors.lineStrong,
          },
          "&:hover .MuiOutlinedInput-notchedOutline": {
            borderColor: colors.lineHover,
          },
          "&.Mui-focused .MuiOutlinedInput-notchedOutline": {
            borderColor: colors.accent,
            borderWidth: 1,
          },
          "&.Mui-focused": {
            boxShadow: `0 0 0 3px ${alpha(colors.accent, 0.24)}`,
          },
          "&.Mui-error .MuiOutlinedInput-notchedOutline": {
            borderColor: colors.dangerInput,
          },
        },
        input: {
          color: colors.text,
          "&::placeholder": {
            color: colors.textPlaceholder,
            opacity: 1,
          },
        },
      },
    },
    MuiInputLabel: {
      styleOverrides: {
        root: {
          color: colors.textSecondary,
          "&.Mui-focused": {
            color: colors.accentText,
          },
          "&.Mui-error": {
            color: colors.dangerText,
          },
        },
      },
    },
    MuiDialog: {
      styleOverrides: {
        paper: {
          backgroundColor: colors.raised,
          border: `1px solid ${colors.lineStrong}`,
          borderRadius: 16,
          backgroundImage: "none",
        },
      },
    },
    MuiBackdrop: {
      styleOverrides: {
        root: {
          backgroundColor: "rgba(6, 5, 4, 0.72)",
        },
      },
    },
    MuiMenu: {
      styleOverrides: {
        paper: {
          backgroundColor: colors.raised,
          border: `1px solid ${colors.lineStrong}`,
          borderRadius: 12,
          boxShadow: "0 18px 40px rgba(0,0,0,0.5)",
        },
      },
    },
    MuiMenuItem: {
      styleOverrides: {
        root: {
          borderRadius: 8,
          "&:hover": {
            backgroundColor: colors.raisedHover,
          },
          "&.Mui-selected": {
            backgroundColor: colors.accentTint,
            "&:hover": {
              backgroundColor: colors.accentTint,
            },
          },
        },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: {
          minHeight: 36,
          borderRadius: 999,
          color: colors.textMuted,
          backgroundColor: "transparent",
          border: `1px solid ${colors.line}`,
          fontWeight: 500,
          "&.Mui-disabled": {
            color: colors.textPlaceholder,
            borderColor: colors.line,
            opacity: 0.72,
          },
          "&.MuiChip-colorPrimary": {
            color: colors.accentText,
            backgroundColor: colors.accentTint,
            borderColor: colors.accentText,
          },
        },
      },
    },
    MuiAlert: {
      styleOverrides: {
        root: {
          alignItems: "center",
          borderRadius: 12,
          backgroundColor: colors.raised,
          color: colors.text,
          border: `1px solid ${colors.lineStrong}`,
          boxShadow: "0 12px 30px rgba(0,0,0,0.4)",
        },
        standardSuccess: {
          borderColor: "#31593E",
          "& .MuiAlert-icon": {
            color: colors.success,
          },
        },
        standardError: {
          borderColor: "#5A2A2C",
          "& .MuiAlert-icon": {
            color: colors.dangerText,
          },
        },
        standardWarning: {
          borderColor: "#665131",
        },
        standardInfo: {
          borderColor: colors.lineStrong,
        },
      },
    },
    MuiTooltip: {
      styleOverrides: {
        tooltip: {
          color: colors.text,
          backgroundColor: colors.raised,
          border: `1px solid ${colors.lineStrong}`,
          borderRadius: 8,
        },
        arrow: {
          color: colors.raised,
        },
      },
    },
  },
});

export default theme;
