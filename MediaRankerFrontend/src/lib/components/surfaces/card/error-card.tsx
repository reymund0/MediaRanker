
import ErrorIcon from "@mui/icons-material/Error";
import { Card, CardContent, CardHeader, Stack, Typography } from "@mui/material";

export interface ErrorCardProps {
  title?: string;
  message: string;
}

export function ErrorCard({ title = "An error occurred", message }: ErrorCardProps) {
  return (
    <Card sx={{ bgcolor: 'error.main' }}>
      <CardHeader sx={{ mt: 2 }} component={() => (
        <Stack direction="row" spacing={2} sx={{ m: 1}}>
          <ErrorIcon fontSize="large" sx={{ color: 'error.contrastText' }} />
          <Typography variant="h6" sx={{ color: 'error.contrastText' }}>{title}</Typography>
        </Stack>
      )} />
      <CardContent>
        <Typography sx={{ color: 'error.contrastText' }}>{message}</Typography>
      </CardContent>
    </Card>
  );
}