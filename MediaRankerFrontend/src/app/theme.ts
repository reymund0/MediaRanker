// theme.ts
import { createTheme } from "@mui/material/styles";
import { LinkBehavior } from "@/lib/components/navigation/linkBehavior";

const theme = createTheme({
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
  },
});

export default theme;
