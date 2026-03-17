
import { Autocomplete, CircularProgress, TextField, TextFieldProps } from "@mui/material";
import { BaseSelectOption } from "../select/base-select";

export type BaseAutocompleteProps = {
  options: BaseSelectOption[];
  isLoading?: boolean;
} & TextFieldProps;

export function BaseAutocomplete({options, isLoading, ...props}: BaseAutocompleteProps) {
  return (
    <Autocomplete<BaseSelectOption>
      fullWidth
      options={options}
      getOptionLabel={(option) => option.label}
      renderInput={(params) => <TextField {...params} {...props} label={isLoading ? undefined : props.label} />}
      disabled={isLoading || props.disabled}
      popupIcon={isLoading ? <CircularProgress size={20} /> : undefined}
    />
  );
}