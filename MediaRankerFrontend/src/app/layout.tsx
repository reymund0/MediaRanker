"use client";
import "@/lib/auth/amplify-config";
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
            <AlertProvider>
              <UserProvider>{children}</UserProvider>
            </AlertProvider>
          </QueryClientProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
