"use client";
import { AmplifyProvider } from "@/lib/amplify-provider";
import { ThemeProvider } from "@mui/material/styles";
import theme from "./theme";

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body>
        <ThemeProvider theme={theme}>
          <AmplifyProvider>
            {children}
          </AmplifyProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
