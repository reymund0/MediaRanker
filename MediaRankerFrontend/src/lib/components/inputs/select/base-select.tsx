import { FormControl, FormHelperText, InputLabel, MenuItem, Select, SelectProps, Typography } from "@mui/material";
import { CircularProgress } from "@mui/material";
import React from 'react';

export type BaseSelectValue = string | number;

export type BaseSelectOption<T = unknown> = {
  id: BaseSelectValue;
  label: string;
  metadata?: T;
}

export type BaseSelectProps<T = unknown> = {
  options: BaseSelectOption<T>[];
  errorMessage?: string;
  isLoading?: boolean;
  renderOptionContent?: (option: BaseSelectOption<T>) => React.ReactNode;
} & Omit<SelectProps<BaseSelectValue>, "labelId" | "error">;

export function BaseSelect<T = unknown>({ 
  options, 
  variant, 
  errorMessage, 
  label, 
  isLoading, 
  renderOptionContent,
  ...props 
}: BaseSelectProps<T>) {

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
        {options.length === 0 && !isLoading ? (
          <MenuItem disabled>
            <Typography variant="body2" color="text.secondary">
              No options available
            </Typography>
          </MenuItem>
        ) : (
          options.map((option) => (
            <MenuItem key={option.id} value={option.id as any}>
              {renderOptionContent ? renderOptionContent(option) : option.label}
            </MenuItem>
          ))
        )}
      </Select>
      {errorMessage && <FormHelperText>{errorMessage}</FormHelperText>}
    </FormControl>
  )
}