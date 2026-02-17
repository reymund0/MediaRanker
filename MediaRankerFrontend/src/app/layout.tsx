"use client";
import { AmplifyProvider } from "@/lib/auth/amplify-provider";
import { UserProvider } from "@/lib/auth/user-provider";
import { AlertProvider } from "@/lib/components/feedback/alert/alert-provider";
import { ThemeProvider } from "@mui/material/styles";
import theme from "./theme";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const queryClient = new QueryClient();

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body>
        <ThemeProvider theme={theme}>
          <QueryClientProvider client={queryClient}>
            <AmplifyProvider>
              <AlertProvider>
                <UserProvider>{children}</UserProvider>
              </AlertProvider>
            </AmplifyProvider>
          </QueryClientProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
