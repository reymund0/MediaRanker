import { FormControl, FormHelperText, InputLabel, MenuItem, Select, SelectProps } from "@mui/material";
import { CircularProgress } from "@mui/material";

export type BaseSelectValue = string | number;

export type BaseSelectOption = {
  id: BaseSelectValue;
  label: string;
}

export type BaseSelectProps = {
  options: BaseSelectOption[];
  errorMessage?: string;
  isLoading?: boolean;
} & Omit<SelectProps<BaseSelectValue>, "labelId" | "error">;

export function BaseSelect({ options, variant, errorMessage, label, isLoading, ...props }: BaseSelectProps) {
  const labelId = `${label || 'select'}-label`;
  return (
    <FormControl
      fullWidth
      variant={variant}
      error={!!errorMessage}
    >
      {/* labelId is needed for MUI Labels to work properly */}
      <InputLabel id={labelId}>{isLoading ? undefined : label}</InputLabel>
      <Select {...props} 
        labelId={labelId} 
        label={label} 
        disabled={isLoading || props.disabled}
        IconComponent={isLoading ? (iconProps) => <CircularProgress {...iconProps} size={20} /> : undefined}
      >
        {options.map((option) => (
          <MenuItem key={option.id} value={option.id}>
            {option.label}
          </MenuItem>
        ))}
      </Select>
      {errorMessage && <FormHelperText>{errorMessage}</FormHelperText>}
    </FormControl>
  )
}