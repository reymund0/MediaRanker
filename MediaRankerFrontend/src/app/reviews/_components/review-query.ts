export const REVIEW_QUERY_ROOT = ["reviews"] as const;

export function reviewQueryKey(mediaType: string) {
  return [...REVIEW_QUERY_ROOT, mediaType] as const;
}

export function reviewQueryOptions(mediaType: string) {
  return {
    route: `/api/reviews/byMediaType/${mediaType}`,
    queryKey: reviewQueryKey(mediaType),
  };
}
