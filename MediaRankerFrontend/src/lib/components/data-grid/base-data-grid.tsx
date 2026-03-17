import { Box, Typography, styled } from "@mui/material";
import {
  DataGrid,
  DataGridProps,
  GridOverlay,
  GridValidRowModel,
} from "@mui/x-data-grid";

const StyledGridOverlay = styled(GridOverlay)(() => ({
  display: "flex",
  flexDirection: "column",
  alignItems: "center",
  justifyContent: "center",
  height: "100%",
}));

function ErrorOverlay() {
  return (
    <StyledGridOverlay>
      <Typography color="error">error loading records</Typography>
    </StyledGridOverlay>
  );
}

interface BaseDataGridProps<
  R extends GridValidRowModel,
> extends DataGridProps<R> {
  error?: boolean;
}

export function BaseDataGrid<R extends GridValidRowModel>(
  props: BaseDataGridProps<R>,
) {
  const { error, rows, ...rest } = props;

  return (
    <Box>
      <DataGrid
        disableRowSelectionOnClick
        hideFooter
        rows={error ? [] : rows}
        slots={{
          noRowsOverlay: error ? ErrorOverlay : undefined,
          ...props.slots,
        }}
        {...rest}
        getRowHeight={() => "auto"}
        sx={{
          border: 0,
          "& .MuiDataGrid-columnHeaders": {
            backgroundColor: "background.default",
            borderBottom: "1px solid",
            borderColor: "divider",
          },
          "& .MuiDataGrid-cell": {
            borderBottomColor: "divider",
            display: "flex",
            alignItems: "center",
            py: 1,
          },
          ...props.sx,
        }}
      />
    </Box>
  );
}
