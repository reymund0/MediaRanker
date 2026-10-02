"use client";

import { forwardRef, useImperativeHandle, useState } from "react";
import {
  Add,
  ChevronRight,
  DeleteOutline,
  EditOutlined,
  ExpandMore,
  MoreHoriz,
} from "@mui/icons-material";
import {
  Alert,
  Box,
  Button,
  ButtonBase,
  CircularProgress,
  Collapse,
  IconButton,
  Menu,
  MenuItem,
  Stack,
  Typography,
} from "@mui/material";
import { useQueryClient } from "@tanstack/react-query";
import { usePagedQuery } from "@/lib/api/use-paged-query";
import { useQuery } from "@/lib/api/use-query";
import { useMutation } from "@/lib/api/use-mutation";
import { usePendingCoverRefresh } from "@/lib/api/use-pending-cover-refresh";
import { useUser } from "@/lib/auth/user-provider";
import { CoverTile } from "@/lib/components/data-display/cover-tile";
import { BaseTextField } from "@/lib/components/inputs/text-field/base-text-field";
import { BaseDialog } from "@/lib/components/feedback/dialog/base-dialog";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import type { PageResult } from "@/lib/contracts/shared";
import type {
  MediaCollectionDto,
  MediaCollectionUpsertRequest,
  MediaDto,
  SeriesRemovalCountsDto,
} from "../contracts";
import type { ReviewDto } from "../../reviews/contracts";
import { buildReviewRankLookups, formatSeriesYearRange, getRankLabel, getReleaseYear, rankableGroup } from "../../reviews/_components/review-utils";
import { buildSeriesUpsertRequest } from "./tv-series-form.mjs";
import { mergeEpisodePages, TV_EPISODE_PAGE_SIZE } from "./tv-episode-utils.mjs";

type Props = {
  search: string;
  onSearchChange: (value: string) => void;
  onReviewSeries: (series: MediaCollectionDto) => void;
  onReviewEpisode: (episode: MediaDto, series: MediaCollectionDto) => void;
  reviews: ReviewDto[];
  onReviewOpen: (id: number) => void;
  onRefresh: () => void;
  revision: number;
  seriesId: string | null;
};

export type TvSeriesListHandle = { openCreate: () => void };

export const TvSeriesList = forwardRef<TvSeriesListHandle, Props>(function TvSeriesList({
  search,
  onSearchChange,
  onReviewSeries,
  onReviewEpisode,
  reviews,
  onReviewOpen,
  onRefresh,
  revision,
  seriesId,
}: Props, ref) {
  const reviewRankLookups = buildReviewRankLookups(reviews);
  const { userId } = useUser();
  const { showSuccess, showError } = useAlert();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(0);
  const [expandedIds, setExpandedIds] = useState<Set<number>>(() => new Set());
  const [linkedSeriesCollapsed, setLinkedSeriesCollapsed] = useState(false);
  const [editing, setEditing] = useState<MediaCollectionDto | null>(null);
  const [isEditorOpen, setIsEditorOpen] = useState(false);
  const [removing, setRemoving] = useState<MediaCollectionDto | null>(null);
  const [title, setTitle] = useState("");
  const [startYear, setStartYear] = useState("");
  const [menu, setMenu] = useState<{ anchor: HTMLElement; series: MediaCollectionDto } | null>(null);
  const { items, totalCount, isLoading, error, refetch } =
    usePagedQuery<MediaCollectionDto>({
      route: "/api/MediaCollection",
      routeParams: { mediaType: "TvShow", collectionType: "Series" },
      queryKey: ["tv-series", search, revision],
      enabled: !!userId,
      pageSize: 10,
      minSearchChars: 0,
      pageRequest: {
        page,
        searchField: "title",
        searchTerm: search,
        sortField: "title",
        sortDirection: "asc",
      },
    });

  usePendingCoverRefresh({
    viewKey: `tv-series:${items.map((item) => item.id).join(",")}`,
    hasPendingCovers: items.some((item) => item.coverStatus === "pending"),
    refetch,
    enabled: !!userId,
  });

  const saveSeries = useMutation<MediaCollectionUpsertRequest, MediaCollectionDto>({
    route: "/api/MediaCollection",
    method: "POST",
  });
  const removeSeries = useMutation<number, void>({
    route: (id) => `/api/MediaCollection/${id}`,
    method: "DELETE",
  });
  const { data: removalCounts, isFetching: countsLoading, error: countsError, refetch: refetchCounts } =
    useQuery<SeriesRemovalCountsDto>({
      route: removing ? `/api/MediaCollection/${removing.id}/removal-counts` : "/api/MediaCollection/0/removal-counts",
      queryKey: ["series-removal-counts", removing?.id],
      enabled: !!removing,
    });

  const openEditor = (series?: MediaCollectionDto) => {
    setIsEditorOpen(true);
    setEditing(series ?? null);
    setTitle(series?.title ?? search);
    setStartYear(String(series?.startYear ?? new Date().getFullYear()));
  };
  useImperativeHandle(ref, () => ({ openCreate: () => openEditor() }));

  const save = async () => {
    const year = Number(startYear);
    if (!title.trim() || !Number.isInteger(year) || year < 1870 || year > new Date().getFullYear()) {
      showError("Enter a series title and a valid start year.");
      return;
    }
    try {
      await saveSeries.mutateAsync(
        buildSeriesUpsertRequest({ id: editing?.id ?? null, title, startYear: year }),
      );
      onSearchChange(title.trim());
      setEditing(null);
      setIsEditorOpen(false);
      await refetch();
      onRefresh();
      showSuccess("Series saved");
    } catch (saveError) {
      showError((saveError as Error).message);
    }
  };

  const confirmRemove = async () => {
    if (!removing || !removalCounts || countsLoading || countsError) return;
    try {
      await removeSeries.mutateAsync(removing.id);
      setRemoving(null);
      setExpandedIds(new Set());
      queryClient.invalidateQueries({ queryKey: ["reviews"] });
      queryClient.invalidateQueries({ queryKey: ["tv-series"] });
      onRefresh();
      showSuccess("Series removed from catalog");
    } catch (removeError) {
      showError((removeError as Error).message);
    }
  };

  return (
    <Stack spacing={2.5}>
      <Stack direction="row" justifyContent="space-between" alignItems="center">
        <Typography variant="caption" color="text.secondary">
          {isLoading ? "LOADING SERIES…" : `${totalCount.toLocaleString()} ${search.trim() ? "SERIES MATCHES" : "TV SERIES"}`}
        </Typography>
      </Stack>
      {error ? <Alert severity="error" action={<Button onClick={() => refetch()}>Try again</Button>}>{error.message}</Alert> : null}
      {isLoading ? <Stack alignItems="center" sx={{ py: 6 }}><CircularProgress size={28} /></Stack> : null}
      {!isLoading && !error && items.length === 0 ? (
        <Stack alignItems="center" spacing={1.5} sx={{ py: 7 }}>
          <Typography variant="h5">No series match “{search}”</Typography>
          <Typography color="text.secondary">Search for a series or add it to the catalog.</Typography>
          <Button startIcon={<Add />} variant="outlined" onClick={() => openEditor()}>Add a title</Button>
        </Stack>
      ) : null}
      <Box sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2, overflow: "hidden" }}>
        <Box sx={{ display: "grid", gridTemplateColumns: "minmax(280px, 1fr) 120px 170px 180px", px: 2.5, py: 1.5, borderBottom: "1px solid", borderColor: "divider" }}>
          <Typography variant="caption">TITLE</Typography>
          <Typography variant="caption">RELEASED</Typography>
          <Typography variant="caption">YOUR SCORE</Typography>
          <Typography variant="caption" textAlign="right">ACTIONS</Typography>
        </Box>
        {items.map((series, index) => {
          const seriesReview = reviews.find((review) => review.kind === "Series" && review.mediaCollectionId === series.id);
          const seriesRankGroup = seriesReview ? reviewRankLookups.get(rankableGroup(seriesReview)) : undefined;
          const seriesRank = seriesReview ? seriesRankGroup?.rankById.get(seriesReview.id) ?? 0 : 0;
          const isExpanded = expandedIds.has(series.id) || (Number(seriesId) === series.id && !linkedSeriesCollapsed);
          const toggleSeries = () => {
            if (Number(seriesId) === series.id) setLinkedSeriesCollapsed(isExpanded);
            setExpandedIds((current) => {
              const next = new Set(current);
              if (isExpanded) next.delete(series.id);
              else next.add(series.id);
              return next;
            });
          };
          return (
            <Box key={series.id} sx={{ borderBottom: index < items.length - 1 ? "1px solid" : undefined, borderColor: "divider" }}>
              <Box sx={{ display: "grid", gridTemplateColumns: "minmax(280px, 1fr) 120px 170px 180px", alignItems: "center", px: 2.5, py: 1.5, minHeight: 88, "&:hover": { bgcolor: "action.hover" } }}>
                <Stack direction="row" alignItems="center" spacing={1.5} sx={{ minWidth: 0 }}>
                  <IconButton aria-label={`${isExpanded ? "Collapse" : "Expand"} ${series.title}`} aria-expanded={isExpanded} onClick={toggleSeries}>
                    {isExpanded ? <ExpandMore /> : <ChevronRight />}
                  </IconButton>
                  <CoverTile title={series.title} src={series.coverImageUrl} status={series.coverStatus} showTitle={false} sx={{ width: 48, height: 72, flexShrink: 0 }} />
                  <ButtonBase onClick={toggleSeries} aria-expanded={isExpanded} sx={{ minWidth: 0, textAlign: "left", display: "block" }}>
                    <Typography fontWeight={700} noWrap>{series.title}</Typography>
                    <Typography variant="caption" color="text.secondary">TV series · {series.seasonCount ?? 0} {(series.seasonCount ?? 0) === 1 ? "season" : "seasons"}</Typography>
                  </ButtonBase>
                </Stack>
                <Typography variant="body2">{formatSeriesYearRange(series.startYear, series.endYear) ?? "—"}</Typography>
                {seriesReview ? <Stack direction="row" spacing={1} alignItems="center"><Typography variant="numeric" color="primary.light">{seriesReview.overallScore}</Typography><Typography variant="caption">{getRankLabel(seriesReview, seriesRank, seriesRankGroup?.totalCount ?? 0)}</Typography></Stack> : <Typography variant="body2" color="text.disabled">Not reviewed</Typography>}
                <Stack direction="row" justifyContent="flex-end" spacing={0.5}>
                  <Button size="small" variant={seriesReview ? "outlined" : "contained"} onClick={() => seriesReview ? onReviewOpen(seriesReview.id) : onReviewSeries(series)}>{seriesReview ? "View review" : "Review series"}</Button>
                  <IconButton aria-label={`Actions for ${series.title}`} onClick={(event) => setMenu({ anchor: event.currentTarget, series })}><MoreHoriz /></IconButton>
                </Stack>
              </Box>
              <Collapse in={isExpanded} unmountOnExit>
                <SeriesSeasons
                  series={series}
                  reviews={reviews}
                  onReviewEpisode={onReviewEpisode}
                  onReviewOpen={onReviewOpen}
                />
              </Collapse>
            </Box>
          );
        })}
      </Box>
      <Menu anchorEl={menu?.anchor} open={!!menu} onClose={() => setMenu(null)}>
        <MenuItem onClick={() => { if (menu) openEditor(menu.series); setMenu(null); }}><EditOutlined fontSize="small" sx={{ mr: 1 }} />Edit details</MenuItem>
        <MenuItem sx={{ color: "error.light" }} onClick={() => { setRemoving(menu?.series ?? null); setMenu(null); }}><DeleteOutline fontSize="small" sx={{ mr: 1 }} />Remove from catalog</MenuItem>
      </Menu>
      <Stack direction="row" justifyContent="space-between" alignItems="center">
        <Typography variant="caption" color="text.secondary">Showing {items.length ? page * 10 + 1 : 0}–{page * 10 + items.length} of {totalCount.toLocaleString()} series</Typography>
        <Stack direction="row" spacing={1}>
          <Button disabled={page === 0} onClick={() => setPage((current) => current - 1)}>Previous</Button>
          <Button disabled={(page + 1) * 10 >= totalCount} onClick={() => setPage((current) => current + 1)}>Next</Button>
        </Stack>
      </Stack>

      <BaseDialog
        open={isEditorOpen}
        title={editing ? "Edit series details" : "Add a TV series"}
        confirmLabel="Save series"
        closeLabel="Cancel"
        confirmLoading={saveSeries.isPending}
        onClose={() => { setEditing(null); setIsEditorOpen(false); }}
        onConfirm={save}
      >
        <Stack spacing={2} sx={{ pt: 1 }}>
          <BaseTextField label="Series title" value={title} onChange={(event) => setTitle(event.target.value)} autoFocus />
          <BaseTextField label="Start year" type="number" value={startYear} onChange={(event) => setStartYear(event.target.value)} />
        </Stack>
      </BaseDialog>
      <BaseDialog
        open={!!removing}
        danger
        title={`Remove “${removing?.title ?? "series"}” and its episodes?`}
        closeLabel="Cancel"
        confirmLabel="Remove series"
        confirmLoading={removeSeries.isPending || countsLoading}
        confirmDisabled={!removalCounts || !!countsError || countsLoading}
        onClose={() => { if (!removeSeries.isPending) setRemoving(null); }}
        onConfirm={confirmRemove}
      >
        {countsError ? <Alert severity="error" action={<Button onClick={() => refetchCounts()}>Retry</Button>}>Couldn’t load removal counts. Try again before removing this series.</Alert> :
          <>This removes the series, all seasons, {removalCounts?.episodeCount ?? "…"} episodes, and {removalCounts?.reviewCount ?? "…"} reviews. This cannot be undone.</>}
      </BaseDialog>
    </Stack>
  );
});

function SeriesSeasons({
  series,
  reviews,
  onReviewEpisode,
  onReviewOpen,
}: {
  series: MediaCollectionDto;
  reviews: Props["reviews"];
  onReviewEpisode: Props["onReviewEpisode"];
  onReviewOpen: Props["onReviewOpen"];
}) {
  const { userId } = useUser();
  const { data, isLoading, isError, refetch } = useQuery<PageResult<MediaCollectionDto>>({
    route: `/api/MediaCollection?mediaType=TvShow&collectionType=Season&parentId=${series.id}&page=0&pageSize=100`,
    queryKey: ["tv-seasons", series.id],
    enabled: !!userId,
  });
  if (isLoading) return <Stack alignItems="center" sx={{ py: 3 }}><CircularProgress size={24} /></Stack>;
  if (isError) return <Alert severity="error" action={<Button onClick={() => refetch()}>Retry</Button>}>Couldn’t load seasons.</Alert>;
  if (!data?.items.length) return <Typography color="text.secondary" sx={{ pl: 10, py: 2 }}>No numbered seasons yet.</Typography>;
  return <Stack sx={{ bgcolor: "background.paper" }}>
    {data.items.map((season) => {
      const reviewedEpisodes = reviews.filter((review) => review.kind === "Episode" && review.seriesId === series.id && review.seasonNumber === season.seasonNumber).length;
      return <SeasonEpisodes key={season.id} season={season} series={series} reviews={reviews} reviewedEpisodes={reviewedEpisodes} onReviewEpisode={onReviewEpisode} onReviewOpen={onReviewOpen} />;
    })}
  </Stack>;
}

function SeasonEpisodes({
  season,
  series,
  reviews,
  reviewedEpisodes,
  onReviewEpisode,
  onReviewOpen,
}: {
  season: MediaCollectionDto;
  series: MediaCollectionDto;
  reviews: Props["reviews"];
  reviewedEpisodes: number;
  onReviewEpisode: Props["onReviewEpisode"];
  onReviewOpen: Props["onReviewOpen"];
}) {
  const { userId } = useUser();
  const [open, setOpen] = useState(false);
      const [episodePage, setEpisodePage] = useState(0);
      const [shownEpisodes, setShownEpisodes] = useState<MediaDto[]>([]);
  const { data, isLoading, isFetching, isError, refetch } = useQuery<PageResult<MediaDto>>({
    route: `/api/media?mediaType=TvShow&mediaCollectionId=${season.id}&sortField=episodeNumber&sortDirection=asc&pageSize=${TV_EPISODE_PAGE_SIZE}&page=${episodePage}`,
    queryKey: ["tv-episodes", season.id, episodePage],
    enabled: !!userId && open,
  });
  const visibleEpisodes = mergeEpisodePages(shownEpisodes, data?.items ?? []);
  const reviewRankLookups = buildReviewRankLookups(reviews);
  const total = data?.totalCount ?? season.episodeCount ?? 0;
  return <Box>
    <Box sx={{ display: "grid", gridTemplateColumns: "minmax(280px, 1fr) 120px 170px 180px", px: 2.5, minHeight: 56, alignItems: "center", borderTop: "1px solid", borderColor: "divider" }}>
    <ButtonBase onClick={() => {
      if (open) { setEpisodePage(0); setShownEpisodes([]); }
      setOpen(!open);
    }} aria-expanded={open} sx={{ width: "100%", justifyContent: "flex-start", gap: 1.5, pl: 7.5, py: 1.25, textAlign: "left" }}>
      {open ? <ExpandMore fontSize="small" /> : <ChevronRight fontSize="small" />}
      <Typography fontWeight={650}>Season {season.seasonNumber}</Typography>
      <Typography variant="caption" color="text.secondary">{(season.episodeCount ?? 0).toLocaleString()} episodes</Typography>
    </ButtonBase>
    <Typography variant="body2" color="text.secondary">{getReleaseYear(season.releaseDate) ?? "—"}</Typography>
    <Typography variant="caption" color="text.secondary">{reviewedEpisodes} of {season.episodeCount ?? 0} reviewed</Typography>
    </Box>
    <Collapse in={open} unmountOnExit>
      {isLoading ? <Stack alignItems="center" sx={{ py: 2 }}><CircularProgress size={20} /></Stack> : null}
      {isError ? <Alert severity="error" action={<Button onClick={() => refetch()}>Retry</Button>}>Couldn’t load episodes.</Alert> : null}
      {visibleEpisodes.map((episode) => {
        const episodeReview = reviews.find((review) => review.kind === "Episode" && review.mediaId === episode.id);
        const episodeRankGroup = episodeReview ? reviewRankLookups.get(rankableGroup(episodeReview)) : undefined;
        const episodeRank = episodeReview ? episodeRankGroup?.rankById.get(episodeReview.id) ?? 0 : 0;
        return <Box key={episode.id} sx={{ display: "grid", gridTemplateColumns: "minmax(280px, 1fr) 120px 170px 180px", alignItems: "center", minHeight: 48, px: 2.5, borderTop: "1px solid", borderColor: "divider" }}>
          <Stack direction="row" spacing={1.5} sx={{ pl: 12.5, minWidth: 0, pr: 2 }}>
          <Typography variant="numeric" color="text.secondary" sx={{ width: 40, flexShrink: 0, fontSize: 13 }}>E{episode.episodeNumber ?? "—"}</Typography>
          <Typography noWrap title={episode.title} variant="body2">{episode.title}</Typography>
          </Stack>
          <Typography variant="body2">{getReleaseYear(episode.releaseDate) ?? "—"}</Typography>
          {episodeReview ? <Stack direction="row" spacing={0.75} alignItems="center"><Typography variant="numeric" color="primary.light">{episodeReview.overallScore}</Typography><Typography variant="caption">{getRankLabel(episodeReview, episodeRank, episodeRankGroup?.totalCount ?? 0)}</Typography></Stack> : <Typography variant="body2" color="text.disabled">Not reviewed</Typography>}
          <Button size="small" sx={{ justifySelf: "end", mr: 4.5 }} variant="outlined" onClick={() => episodeReview ? onReviewOpen(episodeReview.id) : onReviewEpisode(episode, series)}>
            {episodeReview ? "View review" : "Review"}
          </Button>
        </Box>;
      })}
      {visibleEpisodes.length > 0 && visibleEpisodes.length < total ? <Button size="small" disabled={isFetching || isError} onClick={() => { setShownEpisodes(visibleEpisodes); setEpisodePage((current) => current + 1); }}>Show {Math.min(TV_EPISODE_PAGE_SIZE, total - visibleEpisodes.length)} more episodes</Button> : null}
    </Collapse>
  </Box>;
}
