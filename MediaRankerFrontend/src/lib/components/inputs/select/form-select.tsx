import {
  Select,
  SelectProps,
  MenuItem,
  InputLabel,
  FormControl,
  FormHelperText,
} from "@mui/material";
import { Controller, FieldValues, Path, useFormContext } from "react-hook-form";

type FormSelectProps<T extends FieldValues> = {
  name: Path<T>;
  items: { id: string | number; label: string }[];
  label: string;
} & Omit<SelectProps, "name">;

export function FormSelect<T extends FieldValues>({
  name,
  items,
  label,
  variant,
  fullWidth,
  ...rest
}: FormSelectProps<T>) {
  const { control } = useFormContext<T>();
  const labelId = `${name}-label`;
  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState }) => (
        <FormControl
          fullWidth={fullWidth}
          variant={variant}
          error={!!fieldState.error}
        >
          {/* labelId is needed for MUI Labels to work properly */}
          <InputLabel id={labelId}>{label}</InputLabel>
          <Select {...rest} {...field} labelId={labelId} label={label}>
            {items.map((item) => (
              <MenuItem key={item.id} value={item.id}>
                {item.label}
              </MenuItem>
            ))}
          </Select>
          {fieldState.error && (
            <FormHelperText>{fieldState.error.message}</FormHelperText>
          )}
        </FormControl>
      )}
    />
  );
}
