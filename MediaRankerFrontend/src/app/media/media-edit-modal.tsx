import { zodResolver } from "@hookform/resolvers/zod";
import { Box, Stack } from "@mui/material";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { FormDialog } from "@/lib/components/feedback/dialog/form-dialog";
import { FormSelect } from "@/lib/components/inputs/select/form-select";
import { FormTextField } from "@/lib/components/inputs/text-field/form-text-field";
import { FormDatePicker } from "@/lib/components/date-picker/form-date-picker";
import { FormFileUpload } from "@/lib/components/file-upload/form-file-upload";
import { useMutation } from "@/lib/api/use-mutation";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import {
  MediaUpsertRequest,
  GenerateUploadCoverUrlRequest,
  GenerateUploadCoverUrlResponse,
} from "./contracts";
import { MediaTypeDto } from "@/lib/contracts/shared";
import { MediaRow } from "./grid-utils";

const mediaEditSchema = z.object({
  id: z.number().optional(),
  title: z.string().trim().min(1, "Media title is required"),
  mediaTypeId: z.number("Media type is required"),
  releaseDate: z.date("Valid release date is required"),
  coverUploadId: z.number().optional(),
});

type MediaEditFormValues = z.infer<typeof mediaEditSchema>;

type MediaEditModalProps = {
  open: boolean;
  row: MediaRow;
  mediaTypes: MediaTypeDto[];
  onSubmit: (data: MediaUpsertRequest) => void;
  onCancel: () => void;
};

export function MediaEditModal({
  open,
  row,
  mediaTypes,
  onSubmit,
  onCancel,
}: MediaEditModalProps) {
  const methods = useForm<MediaEditFormValues>({
    resolver: zodResolver(mediaEditSchema),
    defaultValues: {
      id: row.id,
      title: row.title,
      mediaTypeId: row.mediaType.id,
      releaseDate: row.releaseDate ?? undefined,
      coverUploadId: undefined,
    },
    mode: "onChange",
  });

  const { handleSubmit } = methods;
  const { showSuccess, showError } = useAlert();

  const { mutateAsync: generateUploadUrl } = useMutation<
    GenerateUploadCoverUrlRequest,
    GenerateUploadCoverUrlResponse
  >({
    route: "/api/media/UploadCover",
    method: "POST",
  });

  const { mutateAsync: completeUpload } = useMutation<number, void>({
    route: (uploadId) => `/api/media/CompleteUploadCover/${uploadId}`,
    method: "POST",
  });

  const handleGenerateUploadUrl = async (file: File) => {
    return await generateUploadUrl({
      mediaId: row.id || null,
      fileName: file.name,
      contentType: file.type,
      fileSizeBytes: file.size,
    });
  };

  const handleCompleteUpload = async (uploadId: number) => {
    await completeUpload(uploadId);
  };

  const onUploadError = (message: string) => {
    showError(message);
  };

  const onSubmitClick = (data: MediaEditFormValues) => {
    onSubmit({
      id: data.id || null,
      title: data.title.trim(),
      mediaTypeId: data.mediaTypeId,
      releaseDate: data.releaseDate.toISOString().slice(0, 10),
      coverUploadId: data.coverUploadId,
    });
  };

  console.log(row);

  return (
    <FormDialog<MediaEditFormValues>
      open={open}
      title={`${row.id ? "Edit" : "Add"} Media`}
      onSubmit={handleSubmit(onSubmitClick)}
      onCancel={onCancel}
      methods={methods}
    >
      <Stack spacing={2} sx={{ mt: 1 }}>
        <FormTextField<MediaEditFormValues> name="title" label="Title" />
        <Stack direction="row" spacing={2}>
          <FormDatePicker<MediaEditFormValues>
            name="releaseDate"
            label="Release date"
            disableFuture
          />
          <FormSelect<MediaEditFormValues>
            name="mediaTypeId"
            label="Media type"
            options={mediaTypes.map((mediaType) => ({
              id: mediaType.id,
              label: mediaType.name,
            }))}
          />
        </Stack>
        <Box sx={{ display: "flex", justifyContent: "center" }}>
          <FormFileUpload<MediaEditFormValues>
            name="coverUploadId"
            label={`Upload ${row.coverImageUrl ? "New" : ""} Cover`}
            initialPreviewUrl={row.coverImageUrl}
            onGenerateUploadUrl={handleGenerateUploadUrl}
            onCompleteUpload={handleCompleteUpload}
            onUploadSuccess={() =>
              showSuccess("Cover image uploaded successfully")
            }
            onUploadError={onUploadError}
            previewSx={{ height: 200 }}
          />
        </Box>
      </Stack>
    </FormDialog>
  );
}
