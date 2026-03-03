import { Box } from "@mui/material";
import { DataGrid, DataGridProps, GridValidRowModel } from "@mui/x-data-grid";

type BaseDataGridProps<R extends GridValidRowModel> = DataGridProps<R>;

export function BaseDataGrid<R extends GridValidRowModel>(props: BaseDataGridProps<R>) {
  return (
    <Box>
      <DataGrid {...props} 
      sx={{
        border: 0,
        "& .MuiDataGrid-columnHeaders": {
          backgroundColor: "background.default",
          borderBottom: "1px solid",
          borderColor: "divider",
        },
        "& .MuiDataGrid-cell": {
          borderBottomColor: "divider",
          alignItems: "center",
        },
        ...props.sx
      }}/>
    </Box>
  );
}
