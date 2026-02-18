"use client";
import "@/lib/auth/amplify-config";
import { UserProvider } from "@/lib/auth/user-provider";
import { AlertProvider } from "@/lib/components/feedback/alert/alert-provider";
import { BaseLayout } from "@/lib/components/layout/base-layout";
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
      <body style={{ margin: 0 }}>
        <ThemeProvider theme={theme}>
          <QueryClientProvider client={queryClient}>
            <AlertProvider>
              <UserProvider>
                <BaseLayout>
                  {children}
                </BaseLayout>
              </UserProvider>
            </AlertProvider>
          </QueryClientProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
