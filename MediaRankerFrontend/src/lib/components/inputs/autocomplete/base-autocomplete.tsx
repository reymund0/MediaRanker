import {
  Autocomplete,
  CircularProgress,
  ListItem,
  TextField,
  TextFieldProps,
  Typography,
} from "@mui/material";
import { BaseSelectOption } from "../select/base-select";

export type BaseAutocompleteProps<T = unknown> = {
  options: BaseSelectOption<T>[];
  isLoading?: boolean;
  renderOptionContent?: (option: BaseSelectOption<T>) => React.ReactNode;
  onSelectOption?: (option: BaseSelectOption<T> | null) => void;
} & TextFieldProps;

export function BaseAutocomplete<T = unknown>({
  options,
  isLoading,
  renderOptionContent,
  onSelectOption,
  ...props
}: BaseAutocompleteProps<T>) {
  return (
    <Autocomplete<BaseSelectOption<T>>
      fullWidth
      options={options}
      getOptionLabel={(option) => option.label}
      onChange={(_e, value) => onSelectOption?.(value)}
      renderInput={(params) => (
        <TextField
          {...params}
          {...props}
          label={isLoading ? undefined : props.label}
        />
      )}
      disabled={isLoading || props.disabled}
      popupIcon={isLoading ? <CircularProgress size={20} /> : undefined}
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
