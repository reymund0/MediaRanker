import { TextFieldProps } from "@mui/material";
import { DatePicker } from "@mui/x-date-pickers/DatePicker";
import { LocalizationProvider } from "@mui/x-date-pickers/LocalizationProvider";
import { AdapterDateFns } from "@mui/x-date-pickers/AdapterDateFns";

type BaseDatePickerProps = {
  value?: Date | null;
  onChange?: (value: Date | null) => void;
  label?: string;
  disableFuture?: boolean;
  disabled?: boolean;
  textFieldProps?: TextFieldProps;
};

export function BaseDatePicker({
  value,
  onChange,
  textFieldProps,
  ...rest
}: BaseDatePickerProps) {
  return (
    <LocalizationProvider dateAdapter={AdapterDateFns}>
      <DatePicker
        value={value ?? null}
        onChange={(nextValue: Date | null) => onChange?.(nextValue)}
        {...rest}
        slotProps={{
          textField: {
            fullWidth: true,
            ...textFieldProps,
          },
        }}
      />
    </LocalizationProvider>
  );
}
