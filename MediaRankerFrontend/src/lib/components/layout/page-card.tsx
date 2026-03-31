
import { Card, CardContent, CardProps } from "@mui/material";

export interface PageCardProps extends CardProps {
  children: React.ReactNode;
}

export function PageCard({ children, sx, ...props }: PageCardProps) {
  return (
    <Card sx={{ my: 3, maxWidth: "1400px", width: "100%", mx: "auto", ...sx }} {...props}>
      <CardContent sx={{ p: 3 }}>
        {children}
      </CardContent>
    </Card>
  );
}