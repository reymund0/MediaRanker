"use client";

import AddIcon from "@mui/icons-material/Add";
import ArrowBackIosNewIcon from "@mui/icons-material/ArrowBackIosNew";
import ArrowForwardIosIcon from "@mui/icons-material/ArrowForwardIos";
import { Box, CircularProgress, IconButton, Stack, Typography } from "@mui/material";
import { useCallback, useEffect, useRef, useState } from "react";
import { useQuery } from "@/lib/api/use-query";
import { ReviewDto } from "./contracts";
import { ReviewCard } from "./review-card";
import { CARD_WIDTH, CARD_GAP } from "./review-card-utils";

const SCROLL_AMOUNT = (CARD_WIDTH + CARD_GAP) * 3;

export interface ReviewRowProps {
  label: string;
  mediaTypeId: number;
}

export function ReviewRow({ label, mediaTypeId }: ReviewRowProps) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const [canScrollLeft, setCanScrollLeft] = useState(false);
  const [canScrollRight, setCanScrollRight] = useState(false);
  const [reviews, setReviews] = useState<ReviewDto[]>([]);
  const [hasNewCard, setHasNewCard] = useState(false);

  const { data: reviewsData, isLoading, isError, error } = useQuery<ReviewDto[]>({
    route: `/api/reviews/byMediaType/${mediaTypeId}`,
    queryKey: ["reviews", mediaTypeId],
  });

  useEffect(() => {
    if (reviewsData) {
      setReviews(reviewsData);
    }
  }, [reviewsData]);

  const updateScrollState = useCallback(() => {
    const el = scrollRef.current;
    if (!el) return;
    setCanScrollLeft(el.scrollLeft > 0);
    setCanScrollRight(el.scrollLeft + el.clientWidth < el.scrollWidth - 1);
  }, []);

  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    updateScrollState();
    el.addEventListener("scroll", updateScrollState);
    const ro = new ResizeObserver(updateScrollState);
    ro.observe(el);
    return () => {
      el.removeEventListener("scroll", updateScrollState);
      ro.disconnect();
    };
  }, [reviews, hasNewCard, updateScrollState]);

  const scrollLeft = () => {
    scrollRef.current?.scrollBy({ left: -SCROLL_AMOUNT, behavior: "smooth" });
  };

  const scrollRight = () => {
    scrollRef.current?.scrollBy({ left: SCROLL_AMOUNT, behavior: "smooth" });
  };

  const handleAddReview = () => {
    if (hasNewCard) return;
    setHasNewCard(true);
    setTimeout(() => {
      scrollRef.current?.scrollTo({ left: 0, behavior: "smooth" });
    }, 50);
  };

  const handleNewCardSave = (review: ReviewDto) => {
    setHasNewCard(false);
    setReviews((prev) => [review, ...prev]);
  };

  const handleNewCardCancel = () => {
    setHasNewCard(false);
  };

  const handleReviewUpdate = (updated: ReviewDto) => {
    setReviews((prev) => prev.map((r) => (r.id === updated.id ? updated : r)));
  };

  const handleReviewDelete = (reviewId: number) => {
    setReviews((prev) => prev.filter((r) => r.id !== reviewId));
  };

  return (
    <Stack direction="column" gap={1.5}>
      <Stack direction="row" alignItems="center" gap={1}>
        <Typography variant="h6">{label}</Typography>
        <IconButton
          size="small"
          onClick={handleAddReview}
          disabled={hasNewCard}
          color="primary"
          sx={{
            backgroundColor: "action.hover",
            "&:hover": {
              backgroundColor: "primary.main",
              color: "primary.contrastText",
            },
          }}
        >
          <AddIcon fontSize="small" />
        </IconButton>
      </Stack>

      <Stack direction="row" alignItems="center" gap={0.5}>
        <IconButton size="small" onClick={scrollLeft} disabled={!canScrollLeft}>
          <ArrowBackIosNewIcon fontSize="small" />
        </IconButton>

        <Box
          ref={scrollRef}
          sx={{
            display: "flex",
            flexDirection: "row",
            gap: `${CARD_GAP}px`,
            overflowX: "auto",
            flex: 1,
            scrollbarWidth: "none",
            "&::-webkit-scrollbar": { display: "none" },
            py: 1,
          }}
        >
          {isLoading ? (
            <Box sx={{ display: "flex", alignItems: "center", px: 2 }}>
              <CircularProgress size={24} />
            </Box>
          ) : isError ? (
            <Typography color="error" sx={{ px: 1 }}>
              {error?.message ?? "Failed to load reviews"}
            </Typography>
          ) : (
            <>
              {hasNewCard && (
                <ReviewCard
                  mediaTypeId={mediaTypeId}
                  onInsertReview={handleNewCardSave}
                  onCancelInsertReview={handleNewCardCancel}
                  onUpdateReview={() => {}}
                  onDeleteReview={() => {}}
                />
              )}
              {reviews.map((review) => (
                <ReviewCard
                  key={review.id}
                  review={review}
                  mediaTypeId={mediaTypeId}
                  onInsertReview={() => {}}
                  onCancelInsertReview={() => {}}
                  onUpdateReview={handleReviewUpdate}
                  onDeleteReview={handleReviewDelete}
                />
              ))}
              {!hasNewCard && reviews.length === 0 && (
                <Typography color="text.secondary" sx={{ px: 1, py: 2 }}>
                  No reviews yet — click + to add one.
                </Typography>
              )}
            </>
          )}
        </Box>

        <IconButton size="small" onClick={scrollRight} disabled={!canScrollRight}>
          <ArrowForwardIosIcon fontSize="small" />
        </IconButton>
      </Stack>
    </Stack>
  );
}