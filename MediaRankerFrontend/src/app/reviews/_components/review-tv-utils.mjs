const TYPE_PLURALS = {
  VideoGame: "video games",
  Book: "books",
  Movie: "movies",
  TvShow: "TV shows",
  Album: "albums",
  Concert: "concerts",
};

export function rankableGroup(review) {
  return `${review.mediaType}:${review.mediaType === "TvShow" ? review.kind : "Title"}`;
}

export function sortReviewsByRank(reviews) {
  return [...reviews].sort((left, right) => {
    const scoreDifference = right.overallScore - left.overallScore;
    if (scoreDifference !== 0) return scoreDifference;
    const updatedDifference = new Date(right.updatedAt).getTime() - new Date(left.updatedAt).getTime();
    if (updatedDifference !== 0) return updatedDifference;
    return left.id - right.id;
  });
}

export function getReviewGroup(reviews, review) {
  const key = rankableGroup(review);
  return sortReviewsByRank(reviews.filter((item) => rankableGroup(item) === key));
}

export function buildReviewRankLookups(reviews) {
  const groups = new Map();
  for (const review of reviews) {
    const key = rankableGroup(review);
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(review);
  }

  return new Map(
    [...groups].map(([key, group]) => {
      const ranked = sortReviewsByRank(group);
      return [key, {
        totalCount: ranked.length,
        rankById: new Map(ranked.map((item, index) => [item.id, index + 1])),
      }];
    }),
  );
}

export function formatSeriesYearRange(startYear, endYear, { includeEndOnly = false } = {}) {
  if (startYear == null) {
    return includeEndOnly && endYear != null ? `–${endYear}` : null;
  }
  if (endYear == null || startYear === endYear) return String(startYear);
  return `${startYear}–${endYear}`;
}

export function getRankLabel(review, rank, total) {
  if (review.mediaType === "TvShow") {
    return `#${rank} of ${total} ${review.kind === "Series" ? "TV series" : "TV episodes"}`;
  }
  return `#${rank} of ${total} ${TYPE_PLURALS[review.mediaType] ?? review.mediaType}`;
}

export function getEpisodeContextLine(review) {
  if (review.kind !== "Episode" || !review.seriesTitle) return null;
  return `${review.seriesTitle} · Season ${review.seasonNumber ?? "—"}, Episode ${review.episodeNumber ?? "—"}`;
}

export function buildReviewTarget(media, series) {
  return series && !media
    ? { mediaId: null, mediaCollectionId: series.id }
    : { mediaId: media?.id ?? null, mediaCollectionId: null };
}
