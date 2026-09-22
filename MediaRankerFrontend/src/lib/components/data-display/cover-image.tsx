"use client";

import ImageNotSupportedIcon from "@mui/icons-material/ImageNotSupported";
import { Box, SxProps, Theme } from "@mui/material";
import { useState } from "react";

type CoverImageProps = {
  src: string | null | undefined;
  alt: string;
  sx?: SxProps<Theme>;
  placeholderSx?: SxProps<Theme>;
};

/** Displays a lazy provider image or a stable accessible fallback after an image error. */
export function CoverImage({ src, alt, sx, placeholderSx }: CoverImageProps) {
  return (
    <CoverImageContent
      key={src ?? "placeholder"}
      src={src}
      alt={alt}
      sx={sx}
      placeholderSx={placeholderSx}
    />
  );
}

function CoverImageContent({ src, alt, sx, placeholderSx }: CoverImageProps) {
  const [failedUrl, setFailedUrl] = useState<string | null>(null);

  if (!src || failedUrl === src) {
    return (
      <Box
        role="img"
        aria-label={`${alt} cover unavailable`}
        sx={{
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          color: "text.disabled",
          ...placeholderSx,
        }}
      >
        <ImageNotSupportedIcon aria-hidden="true" />
      </Box>
    );
  }

  return (
    <Box
      component="img"
      src={src}
      alt={alt}
      loading="lazy"
      onError={() => setFailedUrl(src)}
      sx={sx}
    />
  );
}
