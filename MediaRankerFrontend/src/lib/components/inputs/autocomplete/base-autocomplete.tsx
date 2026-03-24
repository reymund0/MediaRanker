import { Autocomplete, Box, CircularProgress, ListItem, TextField, TextFieldProps, Typography } from "@mui/material";
import { BaseSelectOption } from "../select/base-select";

export type BaseAutocompleteOption = BaseSelectOption & {
  imageUrl?: string | null;
};

export type BaseAutocompleteProps = {
  options: BaseAutocompleteOption[];
  isLoading?: boolean;
} & TextFieldProps;

export function BaseAutocomplete({ options, isLoading, ...props }: BaseAutocompleteProps) {
  return (
    <Autocomplete<BaseAutocompleteOption>
      fullWidth
      options={options}
      getOptionLabel={(option) => option.label}
      renderInput={(params) => <TextField {...params} {...props} label={isLoading ? undefined : props.label} />}
      disabled={isLoading || props.disabled}
      popupIcon={isLoading ? <CircularProgress size={20} /> : undefined}
      renderOption={(optionProps, option) => {
        const { key, ...restOptionProps } = optionProps;

        return (
          <ListItem key={key} {...restOptionProps}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, minHeight: 40 }}>
              {option.imageUrl ? (
                <Box
                  component="img"
                  src={option.imageUrl}
                  alt={option.label}
                  sx={{
                    width: 32,
                    height: 32,
                    objectFit: "cover",
                    borderRadius: 1,
                  }}
                />
              ) : null}
              <Typography variant="body2" noWrap>
                {option.label}
              </Typography>
            </Box>
          </ListItem>
        );
      }}
    />
  );
}