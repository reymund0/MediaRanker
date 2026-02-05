import { AmplifyProvider } from "@/lib/amplifyProvider";

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body>
        <AmplifyProvider>{children}</AmplifyProvider>
      </body>
    </html>
  );
}
