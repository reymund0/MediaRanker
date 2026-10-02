"use client";

import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import SearchIcon from "@mui/icons-material/Search";
import { Alert, Box, Button, ButtonBase, CircularProgress, Stack, Typography } from "@mui/material";
import { useState } from "react";
import { usePagedQuery } from "@/lib/api/use-paged-query";
import { useQuery } from "@/lib/api/use-query";
import { useUser } from "@/lib/auth/user-provider";
import { CoverTile } from "@/lib/components/data-display/cover-tile";
import { BaseTextField } from "@/lib/components/inputs/text-field/base-text-field";
import { MediaTypeChips } from "@/lib/components/inputs/media-type-chips";
import { MediaType, PageResult } from "@/lib/contracts/shared";
import { MediaCollectionDto, MediaDto } from "../../media/contracts";
import { ReviewDto } from "../contracts";
import { mergeEpisodePages, TV_EPISODE_PAGE_SIZE } from "../../media/media-utils";

export function TvSeriesPicker({
  search,
  onSearch,
  browsingSeries,
  onBrowse,
  reviews,
  disabled,
  onMediaTypeChange,
  onSelectSeries,
  onSelectEpisode,
}: {
  search: string;
  onSearch: (value: string) => void;
  browsingSeries: MediaCollectionDto | null;
  onBrowse: (series: MediaCollectionDto | null) => void;
  reviews: ReviewDto[];
  disabled: boolean;
  onMediaTypeChange: (type: MediaType) => void;
  onSelectSeries: (series: MediaCollectionDto) => void;
  onSelectEpisode: (episode: MediaDto, series: MediaCollectionDto) => void;
}) {
  const { userId } = useUser();
  const { items, totalCount, isLoading, error, refetch } = usePagedQuery<MediaCollectionDto>({
    route: "/api/MediaCollection",
    routeParams: { mediaType: "TvShow", collectionType: "Series" },
    queryKey: ["tv-series-picker", search],
    enabled: !!userId && !browsingSeries,
    pageSize: 10,
    minSearchChars: 1,
    pageRequest: { page: 0, searchField: "title", searchTerm: search.trim(), sortField: "title", sortDirection: "asc" },
  });
  const seriesReview = browsingSeries
    ? reviews.find((review) => review.kind === "Series" && review.mediaCollectionId === browsingSeries.id)
    : undefined;

  return (
    <Stack spacing={2} sx={{ minHeight: 0, flex: 1 }}>
      {browsingSeries ? (
        <>
          <Button onClick={() => onBrowse(null)} sx={{ alignSelf: "flex-start" }}>← All TV series</Button>
          <Stack direction="row" spacing={2} alignItems="center" sx={{ p: 1.5, border: "1px solid", borderColor: "divider", borderRadius: 2 }}>
            <CoverTile title={browsingSeries.title} src={browsingSeries.coverImageUrl} status={browsingSeries.coverStatus} sx={{ width: 52, height: 78 }} />
            <Box sx={{ minWidth: 0, flex: 1 }}>
              <Typography variant="h6" noWrap>{browsingSeries.title}</Typography>
              <Typography variant="caption" color="text.secondary">{browsingSeries.seasonCount ?? 0} seasons · {browsingSeries.episodeCount ?? 0} episodes</Typography>
            </Box>
            <Button variant="contained" disabled={disabled || !!seriesReview} onClick={() => onSelectSeries(browsingSeries)}>
              {seriesReview ? `Series reviewed · ${seriesReview.overallScore}` : "Review series"}
            </Button>
          </Stack>
          <Typography variant="overline" color="text.secondary">Or pick an episode</Typography>
          <TvPickerSeasons series={browsingSeries} reviews={reviews} disabled={disabled} onSelectEpisode={onSelectEpisode} />
        </>
      ) : (
        <>
          <MediaTypeChips value={MediaType.TvShow} onChange={onMediaTypeChange} />
          <BaseTextField
            autoFocus
            type="search"
            value={search}
            onChange={(event) => onSearch(event.target.value)}
            placeholder="Search TV series"
            aria-label="Search TV series"
            InputProps={{ startAdornment: <SearchIcon fontSize="small" sx={{ mr: 1, color: "text.secondary" }} /> }}
            sx={{ "& .MuiOutlinedInput-root": { height: 56, bgcolor: "background.default" } }}
          />
          <Typography variant="caption" color="text.secondary">
            {search.trim() ? `${totalCount} series matches for “${search.trim()}”` : "Search for a series to review"}
          </Typography>
          <Box sx={{ flex: 1, minHeight: 0, overflow: "auto" }}>
            {error ? <Alert severity="error" action={<Button onClick={() => refetch()}>Retry</Button>}>{error.message}</Alert> : null}
            {isLoading ? <Stack alignItems="center" sx={{ py: 5 }}><CircularProgress size={28} /></Stack> : null}
            {!isLoading && !error && search.trim() && items.length === 0 ? <Typography color="text.secondary" sx={{ py: 4, textAlign: "center" }}>No series match “{search.trim()}”.</Typography> : null}
            {items.map((series) => {
              const reviewed = reviews.find((review) => review.kind === "Series" && review.mediaCollectionId === series.id);
              return (
                <ButtonBase key={series.id} onClick={() => onBrowse(series)} sx={{ display: "flex", width: "100%", justifyContent: "flex-start", textAlign: "left", gap: 1.5, p: 1, borderRadius: 1.5, "&:hover": { bgcolor: "action.hover" } }}>
                  <CoverTile title={series.title} src={series.coverImageUrl} status={series.coverStatus} sx={{ width: 40, height: 60 }} />
                  <Stack sx={{ flex: 1, minWidth: 0 }}>
                    <Typography fontWeight={650} noWrap>{series.title}</Typography>
                    <Typography variant="caption" color="text.secondary">{series.startYear ?? "TV series"} · {series.seasonCount ?? 0} seasons</Typography>
                  </Stack>
                  {reviewed ? <Typography variant="caption" color="text.secondary">Series reviewed · {reviewed.overallScore}</Typography> : <ChevronRightIcon color="action" />}
                </ButtonBase>
              );
            })}
          </Box>
        </>
      )}
    </Stack>
  );
}

function TvPickerSeasons({
  series,
  reviews,
  disabled,
  onSelectEpisode,
}: {
  series: MediaCollectionDto;
  reviews: ReviewDto[];
  disabled: boolean;
  onSelectEpisode: (episode: MediaDto, series: MediaCollectionDto) => void;
}) {
  const { userId } = useUser();
  const { data, isLoading, isError, refetch } = useQuery<PageResult<MediaCollectionDto>>({
    route: `/api/MediaCollection?mediaType=TvShow&collectionType=Season&parentId=${series.id}&page=0&pageSize=100`,
    queryKey: ["tv-seasons-picker", series.id],
    enabled: !!userId,
  });
  if (isLoading) return <Stack alignItems="center" sx={{ py: 3 }}><CircularProgress size={24} /></Stack>;
  if (isError) return <Alert severity="error" action={<Button onClick={() => refetch()}>Retry</Button>}>Couldn’t load seasons.</Alert>;
  if (!data?.items.length) return <Typography color="text.secondary">No numbered seasons are available.</Typography>;
  return <Stack sx={(theme) => ({ overflow: "auto", minHeight: 0, border: "1px solid", borderColor: "divider", borderRadius: 1.5, scrollbarWidth: "thin", scrollbarColor: `${theme.palette.divider} transparent` })}>
    {data.items.map((season) => <TvPickerSeason key={season.id} series={series} season={season} reviews={reviews} disabled={disabled} onSelectEpisode={onSelectEpisode} />)}
  </Stack>;
}

function TvPickerSeason({
  series,
  season,
  reviews,
  disabled,
  onSelectEpisode,
}: {
  series: MediaCollectionDto;
  season: MediaCollectionDto;
  reviews: ReviewDto[];
  disabled: boolean;
  onSelectEpisode: (episode: MediaDto, series: MediaCollectionDto) => void;
}) {
  const { userId } = useUser();
  const [open, setOpen] = useState(false);
  const [page, setPage] = useState(0);
  const [shown, setShown] = useState<MediaDto[]>([]);
  const { data, isLoading, isFetching, isError, refetch } = useQuery<PageResult<MediaDto>>({
    route: `/api/media?mediaType=TvShow&mediaCollectionId=${season.id}&sortField=episodeNumber&sortDirection=asc&pageSize=${TV_EPISODE_PAGE_SIZE}&page=${page}`,
    queryKey: ["tv-picker-episodes", season.id, page],
    enabled: !!userId && open,
  });
  const visibleEpisodes = mergeEpisodePages(shown, data?.items ?? []);
  const total = data?.totalCount ?? season.episodeCount ?? 0;
  const reviewedCount = reviews.filter((review) => review.kind === "Episode" && review.seriesId === series.id && review.seasonNumber === season.seasonNumber).length;
  return (
    <Box sx={{ borderBottom: "1px solid", borderColor: "divider", "&:last-child": { borderBottom: 0 } }}>
      <Button onClick={() => {
        if (open) { setPage(0); setShown([]); }
        setOpen((value) => !value);
      }} aria-expanded={open} fullWidth startIcon={<ChevronRightIcon sx={{ transform: open ? "rotate(90deg)" : "none" }} />} sx={{ justifyContent: "flex-start", px: 2, py: 1.5, borderRadius: 0 }}>
        Season {season.seasonNumber} · {season.episodeCount ?? 0} episodes
        <Typography component="span" variant="caption" color="text.secondary" sx={{ ml: "auto", pl: 2 }}>{season.releaseDate?.slice(0, 4)} · {reviewedCount} reviewed</Typography>
      </Button>
      {open ? <Box sx={{ pl: 2, bgcolor: "background.default" }}>
        {isLoading ? <CircularProgress size={20} /> : null}
        {isError ? <Alert severity="error" action={<Button onClick={() => refetch()}>Retry</Button>}>Couldn’t load episodes.</Alert> : null}
        {visibleEpisodes.map((episode) => {
          const review = reviews.find((item) => item.kind === "Episode" && item.mediaId === episode.id);
          return <ButtonBase key={episode.id} disabled={disabled || !!review} onClick={(event) => {
            // Appended rows can move under the second click on Show more.
            if (event.detail > 1) return;
            onSelectEpisode(episode, series);
          }} sx={{ display: "flex", width: "100%", justifyContent: "flex-start", textAlign: "left", gap: 1.5, px: 1, py: 1, borderRadius: 1, opacity: review ? 0.55 : 1, "&:hover:not(:disabled)": { bgcolor: "action.hover" } }}>
            <Typography variant="numeric" color="text.secondary" sx={{ minWidth: 44 }}>E{episode.episodeNumber ?? "—"}</Typography>
            <Typography noWrap sx={{ flex: 1 }}>{episode.title}</Typography>
            {review ? <Typography variant="caption" color="text.secondary">Reviewed · {review.overallScore}</Typography> : <ChevronRightIcon color="action" fontSize="small" />}
          </ButtonBase>;
        })}
        {visibleEpisodes.length > 0 && visibleEpisodes.length < total ? <Button size="small" disabled={isFetching || isError} onClick={() => { setShown(visibleEpisodes); setPage((current) => current + 1); }}>Show {Math.min(TV_EPISODE_PAGE_SIZE, total - visibleEpisodes.length)} more episodes</Button> : null}
      </Box> : null}
    </Box>
  );
}
