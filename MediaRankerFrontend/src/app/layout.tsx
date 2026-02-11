"use client";
import { AmplifyProvider } from "@/lib/amplify-provider";
import { UserProvider } from "@/lib/hooks/user-context";
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
            <UserProvider>
              {children}
            </UserProvider>
          </AmplifyProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
