import React from "react";
import {
  Autocomplete,
  CircularProgress,
  ListItem,
  TextField,
  TextFieldProps,
  Typography,
} from "@mui/material";
import { BaseSelectOption } from "../select/base-select";

type TextFieldPassthroughProps = Omit<
  TextFieldProps,
  "value" | "defaultValue" | "onChange" | "select"
>;

export type BaseAutocompleteProps<T = unknown> = {
  options: BaseSelectOption<T>[];
  onSelectOption?: (option: BaseSelectOption<T> | null) => void;
  renderOptionContent?: (option: BaseSelectOption<T>) => React.ReactNode;
  isLoading?: boolean;
  searchInput: string;
  onSearchChange: (input: string) => void;
} & TextFieldPassthroughProps;

export function BaseAutocomplete<T = unknown>({
  options,
  onSelectOption,
  renderOptionContent,
  isLoading,
  searchInput,
  onSearchChange,
  disabled,
  ...props
}: BaseAutocompleteProps<T>) {
  return (
    <Autocomplete<BaseSelectOption<T>>
      fullWidth
      options={options}
      getOptionLabel={(option) => option.label}
      onChange={(_e, val) => onSelectOption?.(val)}
      disabled={disabled}
      popupIcon={isLoading ? <CircularProgress size={20} /> : undefined}
      inputValue={searchInput}
      onInputChange={(_e, val, reason) => {
        // Only trigger search when user is typing or clearing.
        if (reason === "input" || reason === "clear") {
          onSearchChange(val);
        }
      }}
      filterOptions={(x) => x}
      renderInput={(params) => (
        <TextField
          {...params}
          {...props}
          label={isLoading ? undefined : props.label}
        />
      )}
      renderOption={(optionProps, option) => {
        const { key, ...restOptionProps } = optionProps;

        return (
          <ListItem key={key} {...restOptionProps}>
            {renderOptionContent ? (
              renderOptionContent(option)
            ) : (
              <Typography variant="body2" noWrap>
                {option.label}
              </Typography>
            )}
          </ListItem>
        );
      }}
    />
  );
}
