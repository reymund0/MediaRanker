import { useState, useRef, ChangeEvent, useEffect } from "react";
import {
  CircularProgress,
  Stack,
  Typography,
  ButtonProps,
  Box,
  Card,
} from "@mui/material";
import CloudUploadIcon from "@mui/icons-material/CloudUpload";
import ImageIcon from "@mui/icons-material/Image";
import { PrimaryButton } from "@/lib/components/inputs/button/primary-button";

export interface BaseFileUploadProps extends Omit<ButtonProps, "onChange"> {
  onGenerateUploadUrl: (
    file: File,
  ) => Promise<{ url: string; uploadId: number }>;
  onCompleteUpload: (uploadId: number) => Promise<void>;
  onUploadSuccess?: (uploadId: number) => void;
  onUploadError?: (message: string) => void;
  label?: string;
  uploadingLabel?: string;
  initialPreviewUrl?: string;
  previewSx?: React.CSSProperties;
}

export function BaseFileUpload({
  onGenerateUploadUrl,
  onCompleteUpload,
  onUploadSuccess,
  onUploadError,
  label = "Upload File",
  uploadingLabel = "Uploading...",
  initialPreviewUrl,
  previewSx,
  disabled,
  ...buttonProps
}: BaseFileUploadProps) {
  const [isUploading, setIsUploading] = useState(false);
  const [previewUrl, setPreviewUrl] = useState<string | null>(
    initialPreviewUrl || null,
  );
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (initialPreviewUrl) {
      setPreviewUrl(initialPreviewUrl);
    }
  }, [initialPreviewUrl]);

  const handleFileChange = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    // Create local preview if it's an image
    if (file.type.startsWith("image/")) {
      const localUrl = URL.createObjectURL(file);
      setPreviewUrl(localUrl);
    }

    setIsUploading(true);
    try {
      // 1. Generate Upload URL
      const { url, uploadId } = await onGenerateUploadUrl(file);

      // 2. Perform Direct Upload (S3/Pre-signed URL)
      const uploadResponse = await fetch(url, {
        method: "PUT",
        body: file,
        headers: {
          "Content-Type": file.type,
        },
      });

      if (!uploadResponse.ok) {
        throw new Error(`Upload failed with status: ${uploadResponse.status}`);
      }

      // 3. Complete Upload
      await onCompleteUpload(uploadId);

      // 4. Success callback
      onUploadSuccess?.(uploadId);
    } catch (error: unknown) {
      const errorMessage = error instanceof Error ? error.message : "An error occurred during upload.";
      console.error("FileUpload error:", error);
      onUploadError?.(errorMessage);
      // Reset preview on error if it was a new local one
      if (previewUrl && previewUrl.startsWith("blob:")) {
        setPreviewUrl(initialPreviewUrl || null);
      }
    } finally {
      setIsUploading(false);
      // Reset file input so same file can be selected again
      if (fileInputRef.current) {
        fileInputRef.current.value = "";
      }
    }
  };

  return (
    <Stack spacing={2} alignItems="center">
      {previewUrl ? (
        <Box
          component="img"
          src={previewUrl}
          sx={{
            width: "100%",
            height: "100%",
            objectFit: "scale-down",
            ...previewSx,
          }}
        />
      ) : (
        <Card
          variant="outlined"
          sx={{
            width: "100%",
            height: "100%",
            ...previewSx,
          }}
        >
          <Stack
            alignItems="center"
            spacing={1}
            sx={{
              width: "100%",
              height: "100%",
              my: 5,
              color: "text.disabled",
            }}
          >
            <ImageIcon sx={{ fontSize: 48 }} />
            <Typography variant="caption">No image selected</Typography>
          </Stack>
        </Card>
      )}
      {isUploading && (
        <Box
          sx={{
            position: "absolute",
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            bgcolor: "rgba(0, 0, 0, 0.5)",
            zIndex: 1,
          }}
        >
          <CircularProgress color="primary" />
        </Box>
      )}

      <Stack direction="row" alignItems="center" spacing={2}>
        <PrimaryButton
          component="label"
          startIcon={<CloudUploadIcon />}
          disabled={disabled || isUploading}
          {...buttonProps}
        >
          {isUploading ? uploadingLabel : label}
          <input
            type="file"
            hidden
            ref={fileInputRef}
            onChange={handleFileChange}
            disabled={disabled || isUploading}
            accept="image/*"
          />
        </PrimaryButton>
      </Stack>
    </Stack>
  );
}
