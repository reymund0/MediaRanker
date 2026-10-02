"use client";

import {
  createContext,
  ReactNode,
  useCallback,
  useContext,
  useMemo,
  useState,
} from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useUser } from "@/lib/auth/user-provider";
import { MediaDto } from "@/app/media/contracts";
import { MediaCollectionDto } from "@/app/media/contracts";
import { ReviewDto } from "../contracts";
import { NewReviewDialog } from "./new-review-dialog";
import { ReviewDetailDrawer } from "./review-detail-drawer";
import { reviewQueryKey } from "./review-query";
import { sortReviewsByRank } from "./review-utils";

type ReviewExperienceContextValue = {
  isReviewOpen: boolean;
  openNewReview: (media?: MediaDto, series?: MediaCollectionDto) => void;
  openReview: (review: ReviewDto, edit?: boolean) => void;
};

const ReviewExperienceContext = createContext<
  ReviewExperienceContextValue | undefined
>(undefined);

export function useReviewExperience(): ReviewExperienceContextValue {
  const context = useContext(ReviewExperienceContext);
  if (!context) {
    throw new Error(
      "useReviewExperience must be used within ReviewExperienceProvider",
    );
  }
  return context;
}

export function ReviewExperienceProvider({
  children,
}: {
  children: ReactNode;
}) {
  const { authResolved, isAuthenticated, userId } = useUser();
  const authenticatedUserId =
    authResolved && isAuthenticated ? userId : undefined;

  return (
    <ReviewExperienceState
      key={authenticatedUserId ?? "signed-out"}
      enabled={!!authenticatedUserId}
    >
      {children}
    </ReviewExperienceState>
  );
}

function ReviewExperienceState({
  children,
  enabled,
}: {
  children: ReactNode;
  enabled: boolean;
}) {
  const queryClient = useQueryClient();
  const [newReviewMedia, setNewReviewMedia] = useState<MediaDto | undefined>();
  const [newReviewSeries, setNewReviewSeries] = useState<MediaCollectionDto | undefined>();
  const [isNewReviewOpen, setIsNewReviewOpen] = useState(false);
  const [activeReview, setActiveReview] = useState<ReviewDto | null>(null);
  const [isEditingReview, setIsEditingReview] = useState(false);

  const openNewReview = useCallback(
    (media?: MediaDto, series?: MediaCollectionDto) => {
      if (!enabled) return;
      setActiveReview(null);
      setNewReviewMedia(media);
      setNewReviewSeries(series);
      setIsNewReviewOpen(true);
    },
    [enabled],
  );

  const openReview = useCallback(
    (review: ReviewDto, edit = false) => {
      if (!enabled) return;
      setIsNewReviewOpen(false);
      setActiveReview(review);
      setIsEditingReview(edit);
    },
    [enabled],
  );

  const closeReview = useCallback(() => {
    setActiveReview(null);
    setIsEditingReview(false);
  }, []);

  const reconcileReview = useCallback(
    async (
      review: ReviewDto,
      update: (current: ReviewDto[]) => ReviewDto[],
    ) => {
      const queryKey = reviewQueryKey(review.mediaType);
      await queryClient.cancelQueries({ queryKey, exact: true });
      queryClient.setQueryData<ReviewDto[]>(queryKey, (current) =>
        sortReviewsByRank(update(current ?? [])),
      );
    },
    [queryClient],
  );

  const handleCreated = useCallback(
    async (review: ReviewDto) => {
      await reconcileReview(review, (current) => [
        review,
        ...current.filter((existing) => existing.id !== review.id),
      ]);
      setIsNewReviewOpen(false);
      setNewReviewMedia(undefined);
      setNewReviewSeries(undefined);
    },
    [reconcileReview],
  );

  const handleUpdated = useCallback(
    async (review: ReviewDto) => {
      await reconcileReview(review, (current) => [
        ...current.filter((existing) => existing.id !== review.id),
        review,
      ]);
      setActiveReview(review);
      setIsEditingReview(false);
    },
    [reconcileReview],
  );

  const handleDeleted = useCallback(
    async (review: ReviewDto) => {
      await reconcileReview(review, (current) =>
        current.filter((existing) => existing.id !== review.id),
      );
      closeReview();
    },
    [closeReview, reconcileReview],
  );

  const contextValue = useMemo(
    () => ({ openNewReview, openReview, isReviewOpen: activeReview !== null }),
    [openNewReview, openReview, activeReview],
  );

  return (
    <ReviewExperienceContext.Provider value={contextValue}>
      {children}
      {enabled && isNewReviewOpen ? (
        <NewReviewDialog
          initialMedia={newReviewMedia}
          initialSeries={newReviewSeries}
          onClose={() => {
            setIsNewReviewOpen(false);
            setNewReviewMedia(undefined);
            setNewReviewSeries(undefined);
          }}
          onCreated={handleCreated}
        />
      ) : null}
      {enabled && activeReview ? (
        <ReviewDetailDrawer
          review={activeReview}
          edit={isEditingReview}
          onEdit={() => setIsEditingReview(true)}
          onCancelEdit={() => setIsEditingReview(false)}
          onClose={closeReview}
          onUpdated={handleUpdated}
          onDeleted={handleDeleted}
          onOpenRelatedReview={(review) => {
            setActiveReview(review);
            setIsEditingReview(false);
          }}
        />
      ) : null}
    </ReviewExperienceContext.Provider>
  );
}
