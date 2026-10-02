import { Box, Chip, Typography } from "@mui/material";
import { MediaType } from "@/lib/contracts/shared";

const MEDIA_TYPE_CHIP_LABELS: Record<MediaType, string> = {
  [MediaType.VideoGame]: "Video games",
  [MediaType.Movie]: "Movies",
  [MediaType.TvShow]: "TV shows",
  [MediaType.Book]: "Books",
  [MediaType.Album]: "Albums",
  [MediaType.Concert]: "Concerts",
};

const SECONDARY_MEDIA_TYPES = [MediaType.Book, MediaType.Album, MediaType.Concert];
const MEDIA_TYPE_ORDER = [
  MediaType.VideoGame,
  MediaType.Movie,
  MediaType.TvShow,
  MediaType.Book,
  MediaType.Album,
  MediaType.Concert,
];

export type MediaTypeChipsProps = {
  value: MediaType;
  onChange: (value: MediaType) => void;
  counts?: Partial<Record<MediaType, number>>;
  disabledTypes?: MediaType[];
};

export function MediaTypeChips({
  value,
  onChange,
  counts,
  disabledTypes = [],
}: MediaTypeChipsProps) {
  const groupSecondaryTypes =
    value !== MediaType.Book &&
    value !== MediaType.Album &&
    value !== MediaType.Concert &&
    SECONDARY_MEDIA_TYPES.every((type) => disabledTypes.includes(type));
  const types = MEDIA_TYPE_ORDER.filter(
    (type) => !groupSecondaryTypes || !SECONDARY_MEDIA_TYPES.includes(type),
  );

  return (
    <Box role="group" aria-label="Filter by media type" sx={{ display: "flex", flexWrap: "wrap", gap: 1 }}>
      {types.map((type) => {
        const selected = value === type;
        const count = counts?.[type];

        return (
          <Chip
            key={type}
            clickable
            label={
              <Box sx={{ display: "inline-flex", alignItems: "center", gap: 1 }}>
                <span>{MEDIA_TYPE_CHIP_LABELS[type]}</span>
                {count !== undefined ? (
                  <Typography
                    component="span"
                    variant="numeric"
                    sx={{ fontSize: 11, color: selected ? "primary.light" : "text.muted" }}
                  >
                    {count}
                  </Typography>
                ) : null}
              </Box>
            }
            color={selected ? "primary" : "default"}
            variant="outlined"
            disabled={disabledTypes.includes(type)}
            aria-label={`${MEDIA_TYPE_CHIP_LABELS[type]}${count === undefined ? "" : `, ${count}`}`}
            aria-pressed={selected}
            onClick={() => onChange(type)}
            sx={{
              px: 0.5,
              "&:hover": {
                borderColor: "text.muted",
                backgroundColor: "background.raised",
              },
              ...(selected
                ? {
                    color: "secondary.main",
                    backgroundColor: "action.selected",
                    borderColor: "secondary.main",
                    "&:hover": {
                      backgroundColor: "action.selected",
                    },
                  }
                : {}),
            }}
          />
        );
      })}
      {groupSecondaryTypes ? (
        <Chip
          disabled
          label={
            <Box sx={{ display: "inline-flex", alignItems: "center", gap: 1 }}>
              <span>Books, albums, concerts</span>
              <Typography component="span" variant="caption" color="text.muted">
                none yet
              </Typography>
            </Box>
          }
          variant="outlined"
          sx={{ borderStyle: "dashed" }}
        />
      ) : null}
    </Box>
  );
}
