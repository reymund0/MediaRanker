import { Stack, Typography } from "@mui/material";

interface DateTimeCellProps {
  value: Date | null | undefined;
}

export function DateTimeCell({ value }: DateTimeCellProps) {
  return (
    <Stack sx={{ lineHeight: 1.1 }}>
      <Typography variant="body2" color="text.secondary">
        {value ? value.toLocaleDateString() : "-"}
      </Typography>
      <Typography variant="caption" color="text.disabled">
        {value ? value.toLocaleTimeString() : "-"}
      </Typography>
    </Stack>
  );
}
