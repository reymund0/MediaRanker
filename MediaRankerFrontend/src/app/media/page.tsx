"use client";

import { Suspense, useCallback, useEffect, useRef, useState } from "react";
import { useSearchParams } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import {
  Add,
  Search,
  MoreHoriz,
  EditOutlined,
  DeleteOutline,
  ChevronLeft,
  ChevronRight,
} from "@mui/icons-material";
import {
  Box,
  Button,
  Chip,
  IconButton,
  InputAdornment,
  Menu,
  MenuItem,
  Stack,
  Typography,
} from "@mui/material";
import { usePagedQuery } from "@/lib/api/use-paged-query";
import { useQuery } from "@/lib/api/use-query";
import { useMutation } from "@/lib/api/use-mutation";
import { usePendingCoverRefresh } from "@/lib/api/use-pending-cover-refresh";
import { useUser } from "@/lib/auth/user-provider";
import {
  ALL_MEDIA_TYPES,
  MEDIA_TYPE_LABELS,
  MediaType,
  PageResult,
} from "@/lib/contracts/shared";
import { PageContainer } from "@/lib/components/layout/page-container";
import { BaseTextField } from "@/lib/components/inputs/text-field/base-text-field";
import { CoverTile } from "@/lib/components/data-display/cover-tile";
import { MediaTypeChips } from "@/lib/components/inputs/media-type-chips";
import { BaseDialog } from "@/lib/components/feedback/dialog/base-dialog";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import { ReviewDto } from "../reviews/contracts";
import { useReviewExperience } from "../reviews/_components/review-experience";
import { sortReviewsByRank } from "../reviews/_components/review-utils";
import { MediaDto, MediaUpsertRequest } from "./contracts";
import { MediaEditModal } from "./media-edit-modal";
import { mapMediaToRow, MediaRow } from "./grid-utils";

function TypeAvailability({
  type,
  onResult,
}: {
  type: MediaType;
  onResult: (type: MediaType, available: boolean) => void;
}) {
  const { userId } = useUser();
  const { data } = useQuery<PageResult<MediaDto>>({
    route: `/api/media?mediaType=${type}&pageSize=1`,
    queryKey: ["media-availability", type],
    enabled: !!userId,
    staleTime: 300_000,
  });
  useEffect(() => {
    if (data) onResult(type, data.items.length > 0);
  }, [data, type, onResult]);
  return null;
}

function Catalog() {
  const { userId } = useUser();
  const { showSuccess, showError } = useAlert();
  const queryClient = useQueryClient();
  const { openNewReview, openReview } = useReviewExperience();
  const searchParams = useSearchParams();
  const inputRef = useRef<HTMLInputElement>(null);
  const requestedType = searchParams.get("mediaType") as MediaType | null;
  const [type, setType] = useState<MediaType>(
    requestedType && ALL_MEDIA_TYPES.includes(requestedType)
      ? requestedType
      : MediaType.VideoGame,
  );
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(0);
  const [revision, setRevision] = useState(0);
  const [availability, setAvailability] = useState<
    Partial<Record<MediaType, boolean>>
  >({});
  const [draft, setDraft] = useState<MediaRow>();
  const [removing, setRemoving] = useState<MediaDto>();
  const [menu, setMenu] = useState<{ anchor: HTMLElement; media: MediaDto }>();
  useEffect(() => {
    if (searchParams.has("focus")) inputRef.current?.focus();
  }, [searchParams]);
  const { items, totalCount, isLoading, error, refetch } =
    usePagedQuery<MediaDto>({
      route: "/api/media",
      routeParams: { mediaType: type },
      queryKey: ["media", type, revision],
      enabled: !!userId,
      minSearchChars: 0,
      pageSize: 10,
      pageRequest: {
        page,
        searchField: "title",
        searchTerm: search,
        sortField: "title",
        sortDirection: "asc",
      },
    });
  const { data: reviews = [] } = useQuery<ReviewDto[]>({
    route: `/api/reviews/byMediaType/${type}`,
    queryKey: ["reviews", type],
    enabled: !!userId,
  });
  const ranked = sortReviewsByRank(reviews);
  usePendingCoverRefresh({
    viewKey: `${type}:${items.map((m) => m.id).join(",")}`,
    hasPendingCovers: items.some((m) => m.coverStatus === "pending"),
    refetch,
    enabled: !!userId,
  });
  const save = useMutation<MediaUpsertRequest, MediaDto>({
    route: "/api/media",
    method: "POST",
  });
  const remove = useMutation<number, void>({
    route: (id) => `/api/media/${id}`,
    method: "DELETE",
  });
  const refresh = () => {
    setRevision((value) => value + 1);
    setPage(0);
    queryClient.invalidateQueries({ queryKey: ["media"] });
    queryClient.invalidateQueries({ queryKey: ["media-availability"] });
  };
  const addTitle = () =>
    setDraft({
      id: undefined,
      title: search,
      mediaType: type,
      releaseDate: null,
      createdAt: null,
      updatedAt: null,
      coverStatus: "unsupported",
    });
  const updateAvailability = useCallback(
    (mediaType: MediaType, available: boolean) =>
      setAvailability((previous) =>
        previous[mediaType] === available
          ? previous
          : { ...previous, [mediaType]: available },
      ),
    [],
  );

  return (
    <PageContainer>
      {ALL_MEDIA_TYPES.map((mediaType) => (
        <TypeAvailability
          key={mediaType}
          type={mediaType}
          onResult={updateAvailability}
        />
      ))}
      <Typography variant="overline" color="primary.light">
        Catalog
      </Typography>
      <Stack
        direction="row"
        justifyContent="space-between"
        alignItems="center"
        sx={{ mb: 4 }}
      >
        <Typography variant="h1">Find something to rank</Typography>
        <Button variant="outlined" startIcon={<Add />} onClick={addTitle}>
          Add a title
        </Button>
      </Stack>
      <BaseTextField
        id="catalog-search"
        inputRef={inputRef}
        inputProps={{ "aria-label": "Search the catalog" }}
        placeholder="Search titles…"
        value={search}
        onChange={(e) => {
          setSearch(e.target.value);
          setPage(0);
        }}
        fullWidth
        sx={{ mb: 3, "& .MuiOutlinedInput-root": { height: 64, fontSize: 20 } }}
        slotProps={{
          input: {
            startAdornment: (
              <InputAdornment position="start">
                <Search />
              </InputAdornment>
            ),
            endAdornment: (
              <InputAdornment position="end">
                <Typography
                  component="kbd"
                  variant="numeric"
                  sx={{ fontSize: 12 }}
                >
                  Ctrl K
                </Typography>
              </InputAdornment>
            ),
          },
        }}
      />
      <MediaTypeChips
        value={type}
        onChange={(next) => {
          setType(next);
          setPage(0);
        }}
        disabledTypes={ALL_MEDIA_TYPES.filter(
          (mt) => availability[mt] === false,
        )}
      />
      <Box
        sx={{
          mt: 3.5,
          border: "1px solid",
          borderColor: "divider",
          borderRadius: 1.5,
          overflow: "hidden",
        }}
      >
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: "minmax(280px, 1fr) 120px 170px 220px",
            px: 2.5,
            py: 1.5,
            borderBottom: "1px solid",
            borderColor: "divider",
          }}
        >
          <Typography variant="caption">
            {isLoading
              ? "LOADING…"
              : `${totalCount.toLocaleString()} ${search ? (totalCount === 1 ? "MATCH" : "MATCHES") : totalCount === 1 ? "TITLE" : "TITLES"}`}
          </Typography>
          <Typography variant="caption">RELEASED</Typography>
          <Typography variant="caption">YOUR SCORE</Typography>
          <Typography variant="caption" textAlign="right">
            SORTED BY {search ? "RELEVANCE" : "TITLE"}
          </Typography>
        </Box>
        {error && (
          <Typography role="alert" color="error" sx={{ p: 3 }}>
            {error.message} <Button onClick={() => refetch()}>Try again</Button>
          </Typography>
        )}
        {!isLoading && !error && items.length === 0 && (
          <Stack alignItems="center" spacing={2} sx={{ p: 7 }}>
            <Typography variant="h5">No titles match “{search}”</Typography>
            <Typography color="text.secondary">
              Try a different search, or add the title to the catalog.
            </Typography>
            <Button variant="outlined" startIcon={<Add />} onClick={addTitle}>
              Add a title
            </Button>
          </Stack>
        )}
        {items.map((media) => {
          const rank = ranked.findIndex(
            (review) => review.mediaId === media.id,
          );
          const review = ranked[rank];
          return (
            <Box
              key={media.id}
              sx={{
                display: "grid",
                gridTemplateColumns: "minmax(280px, 1fr) 120px 170px 220px",
                alignItems: "center",
                px: 2.5,
                py: 1.5,
                borderBottom: "1px solid",
                borderColor: "divider",
                "&:hover": { bgcolor: "action.hover" },
              }}
            >
              <Stack direction="row" spacing={2} alignItems="center">
                <CoverTile
                  title={media.title}
                  src={media.coverImageUrl}
                  status={media.coverStatus}
                  showTitle={false}
                  sx={{ width: 48, flexShrink: 0 }}
                />
                <Box>
                  <Typography fontWeight={600}>{media.title}</Typography>
                  <Typography variant="caption" color="text.secondary">
                    {MEDIA_TYPE_LABELS[type].charAt(0) +
                      MEDIA_TYPE_LABELS[type].slice(1).toLowerCase()}
                    {media.coverStatus === "pending"
                      ? " · finding cover art…"
                      : ""}
                  </Typography>
                </Box>
              </Stack>
              <Typography variant="body2">
                {media.releaseDate?.slice(0, 4) ?? "—"}
              </Typography>
              {review ? (
                <Stack direction="row" spacing={1} alignItems="center">
                  <Chip
                    color="primary"
                    size="small"
                    label={review.overallScore}
                  />
                  <Typography variant="caption">
                    #{rank + 1} of {ranked.length}
                  </Typography>
                </Stack>
              ) : (
                <Typography variant="body2" color="text.disabled">
                  Not reviewed
                </Typography>
              )}
              <Stack direction="row" justifyContent="flex-end" spacing={1}>
                <Button
                  variant="outlined"
                  onClick={() =>
                    review ? openReview(review) : openNewReview(media)
                  }
                >
                  {review ? "View review" : "Review"}
                </Button>
                <IconButton
                  aria-label={`Actions for ${media.title}`}
                  aria-haspopup="menu"
                  onClick={(event) =>
                    setMenu({ anchor: event.currentTarget, media })
                  }
                >
                  <MoreHoriz />
                </IconButton>
              </Stack>
            </Box>
          );
        })}
        <Stack
          direction="row"
          justifyContent="space-between"
          alignItems="center"
          sx={{ px: 2.5, py: 1.5 }}
        >
          <Typography variant="caption">
            Showing {items.length ? page * 10 + 1 : 0}–
            {page * 10 + items.length} of {totalCount.toLocaleString()}
          </Typography>
          <Stack direction="row" spacing={1}>
            <IconButton
              aria-label="Previous page"
              disabled={page === 0 || isLoading}
              onClick={() => setPage((p) => p - 1)}
            >
              <ChevronLeft />
            </IconButton>
            <IconButton
              aria-label="Next page"
              disabled={(page + 1) * 10 >= totalCount || isLoading}
              onClick={() => setPage((p) => p + 1)}
            >
              <ChevronRight />
            </IconButton>
          </Stack>
        </Stack>
      </Box>
      <Menu
        disableScrollLock
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
        transformOrigin={{ vertical: "top", horizontal: "right" }}
        slotProps={{ backdrop: { sx: { backgroundColor: "transparent" } } }}
        anchorEl={menu?.anchor}
        open={!!menu}
        onClose={() => setMenu(undefined)}
      >
        <MenuItem
          onClick={() => {
            if (menu) setDraft(mapMediaToRow(menu.media));
            setMenu(undefined);
          }}
        >
          <EditOutlined fontSize="small" sx={{ mr: 1 }} />
          Edit details
        </MenuItem>
        <MenuItem
          sx={{ color: "error.light" }}
          onClick={() => {
            setRemoving(menu?.media);
            setMenu(undefined);
          }}
        >
          <DeleteOutline fontSize="small" sx={{ mr: 1 }} />
          Remove from catalog
        </MenuItem>
      </Menu>
      {draft && (
        <MediaEditModal
          open
          row={draft}
          onCancel={() => setDraft(undefined)}
          onSubmit={async (data) => {
            try {
              await save.mutateAsync(data);
              setSearch(data.title);
              setType(data.mediaType as MediaType);
              refresh();
              setDraft(undefined);
              showSuccess("Title saved");
            } catch (error) {
              showError((error as Error).message);
            }
          }}
        />
      )}
      {removing && (
        <BaseDialog
          open
          danger
          title={`Remove “${removing.title}”?`}
          closeLabel="Cancel"
          confirmLabel="Remove from catalog"
          confirmLoading={remove.isPending}
          onClose={() => {
            if (!remove.isPending) setRemoving(undefined);
          }}
          onConfirm={() =>
            remove.mutate(removing.id, {
              onSuccess: () => {
                refresh();
                queryClient.invalidateQueries({ queryKey: ["reviews"] });
                setRemoving(undefined);
                showSuccess("Title removed");
              },
              onError: (e) => showError(e.message),
            })
          }
        >
          This removes the title and its reviews. This action cannot be undone.
        </BaseDialog>
      )}
    </PageContainer>
  );
}

export default function MediaPage() {
  return (
    <Suspense>
      <Catalog />
    </Suspense>
  );
}
