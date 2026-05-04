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
  | "value"
  | "defaultValue"
  | "onChange"
  | "onBlur"
  | "inputRef"
  | "name"
  | "select"
>;

export type BaseAutocompleteProps<T = unknown> = {
  options: BaseSelectOption<T>[];
  value?: BaseSelectOption<T> | null;
  defaultValue?: BaseSelectOption<T> | null;
  onSelectOption?: (option: BaseSelectOption<T> | null) => void;
  isOptionEqualToValue?: (
    a: BaseSelectOption<T>,
    b: BaseSelectOption<T>,
  ) => boolean;
  renderOptionContent?: (option: BaseSelectOption<T>) => React.ReactNode;
  isLoading?: boolean;
  onBlur?: () => void;
  inputRef?: React.Ref<unknown>;
  name?: string;
  inputValue?: string;
  onSearchChange?: (input: string) => void;
} & TextFieldPassthroughProps;

export function BaseAutocomplete<T = unknown>({
  options,
  value,
  defaultValue,
  onSelectOption,
  isOptionEqualToValue,
  renderOptionContent,
  isLoading,
  onBlur,
  inputRef,
  name,
  inputValue,
  onSearchChange,
  disabled,
  ...props
}: BaseAutocompleteProps<T>) {
  const isAsync = onSearchChange !== undefined;

  return (
    <Autocomplete<BaseSelectOption<T>>
      fullWidth
      options={options}
      getOptionLabel={(option) => option.label}
      value={value !== undefined ? value : undefined}
      defaultValue={defaultValue !== undefined ? defaultValue : undefined}
      onChange={(_e, val) => onSelectOption?.(val)}
      isOptionEqualToValue={
        isOptionEqualToValue ?? ((a, b) => a.id === b.id)
      }
      disabled={disabled || isLoading}
      popupIcon={isLoading ? <CircularProgress size={20} /> : undefined}
      inputValue={inputValue !== undefined ? inputValue : undefined}
      onInputChange={
        isAsync
          ? (_e, val, reason) => {
              if (reason === "input" || reason === "clear") {
                onSearchChange(val);
              }
            }
          : undefined
      }
      filterOptions={isAsync ? (x) => x : undefined}
      renderInput={(params) => (
        <TextField
          {...params}
          {...props}
          label={isLoading ? undefined : props.label}
          name={name}
          onBlur={onBlur}
          slotProps={{ htmlInput: { ...params.inputProps, ref: inputRef } }}
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
