import { CircularProgress, Box, SxProps, Theme, Typography } from "@mui/material";
import { CoverImage } from "@/lib/components/data-display/cover-image";

export type CoverTileStatus =
  | "ready"
  | "pending"
  | "missing"
  | "failed"
  | "disabled"
  | "unsupported";

export type CoverTileProps = {
  title: string;
  src?: string | null;
  status: CoverTileStatus;
  showTitle?: boolean;
  sx?: SxProps<Theme>;
};

export function CoverTile({
  title,
  src,
  status,
  showTitle = true,
  sx,
}: CoverTileProps) {
  const ready = status === "ready" && Boolean(src);
  const label = title.trim() || "Untitled";

  return (
    <Box
      sx={[
        {
          position: "relative",
          width: "100%",
          aspectRatio: "2 / 3",
          overflow: "hidden",
          borderRadius: "10px",
          backgroundColor: "background.raised",
          boxShadow: (theme) => `inset 0 0 0 1px ${theme.palette.divider}`,
        },
        ...(Array.isArray(sx) ? sx : sx ? [sx] : []),
      ]}
    >
      {ready ? (
        <CoverImage
          src={src}
          alt={`${label} cover art`}
          sx={{ position: "absolute", inset: 0, width: "100%", height: "100%", objectFit: "cover" }}
          placeholderSx={{
            position: "absolute",
            inset: 0,
            width: "100%",
            height: "100%",
            color: "text.muted",
          }}
        />
      ) : (
        <>
          <Box aria-hidden="true" sx={{ position: "absolute", inset: 0 }}>
            <CoverImage
              src={null}
              alt=""
              sx={{ width: "100%", height: "100%" }}
              placeholderSx={{
                width: "100%",
                height: "100%",
                color: "transparent",
                "& svg": { display: "none" },
              }}
            />
          </Box>
          {status === "pending" ? (
            <Box
              role="img"
              aria-label={`${label} cover art is being found`}
              sx={{
                position: "absolute",
                inset: 0,
                display: "flex",
                flexDirection: "column",
                alignItems: "center",
                justifyContent: "center",
                gap: 1,
              }}
            >
              <CircularProgress size={20} aria-hidden="true" sx={{ color: "primary.light" }} />
              <Typography aria-hidden="true" variant="caption" color="text.muted" sx={{ fontSize: 11 }}>
                Finding art…
              </Typography>
            </Box>
          ) : (
            <Box
              role="img"
              aria-label={`${label} cover unavailable`}
              sx={{ position: "absolute", inset: 0 }}
            >
              <Typography
                aria-hidden="true"
                sx={{
                  position: "absolute",
                  left: 0,
                  right: 0,
                  top: "50%",
                  transform: "translateY(-50%)",
                  textAlign: "center",
                  fontFamily: "var(--font-display), sans-serif",
                  fontWeight: 800,
                  fontSize: 32,
                  color: "text.placeholder",
                }}
              >
                {label.charAt(0).toUpperCase()}
              </Typography>
              {showTitle ? (
                <Typography
                  aria-hidden="true"
                  variant="caption"
                  sx={{
                    position: "absolute",
                    left: 10,
                    right: 10,
                    bottom: 10,
                    color: "text.muted",
                    fontWeight: 600,
                  }}
                >
                  {label}
                </Typography>
              ) : null}
            </Box>
          )}
        </>
      )}
    </Box>
  );
}
