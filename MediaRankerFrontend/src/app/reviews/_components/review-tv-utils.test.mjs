import test from "node:test";
import assert from "node:assert/strict";
import {
  buildReviewTarget,
  buildReviewRankLookups,
  formatSeriesYearRange,
  getEpisodeContextLine,
  getRankLabel,
  getReviewGroup,
} from "./review-tv-utils.mjs";

const review = (id, kind, score, extra = {}) => ({
  id,
  kind,
  mediaType: "TvShow",
  overallScore: score,
  updatedAt: "2026-09-01T00:00:00Z",
  ...extra,
});

test("TV series and episodes rank independently while non-TV keeps one group", () => {
  const items = [
    review(1, "Series", 9),
    review(2, "Episode", 10),
    review(3, "Episode", 8),
  ];
  assert.deepEqual(getReviewGroup(items, items[0]).map((item) => item.id), [1]);
  assert.deepEqual(getReviewGroup(items, items[1]).map((item) => item.id), [2, 3]);

  const movieA = { ...review(4, "Title", 8), mediaType: "Movie" };
  const movieB = { ...review(5, "Title", 9), mediaType: "Movie" };
  assert.deepEqual(getReviewGroup([movieA, movieB], movieA).map((item) => item.id), [5, 4]);
});

test("TV rank labels include the kind and TV scope", () => {
  assert.equal(getRankLabel(review(1, "Series", 9), 2, 3), "#2 of 3 TV series");
  assert.equal(getRankLabel(review(2, "Episode", 9), 1, 4), "#1 of 4 TV episodes");
  assert.equal(getRankLabel({ ...review(3, "Title", 9), mediaType: "Movie" }, 1, 2), "#1 of 2 movies");
});

test("episode context uses series and numbered location", () => {
  assert.equal(
    getEpisodeContextLine(review(1, "Episode", 9, { seriesTitle: "Breaking Bad", seasonNumber: 1, episodeNumber: 4 })),
    "Breaking Bad · Season 1, Episode 4",
  );
  assert.equal(getEpisodeContextLine(review(2, "Series", 9, { seriesTitle: "Breaking Bad" })), null);
});

test("review requests select exactly one series or media target", () => {
  assert.deepEqual(buildReviewTarget(null, { id: 44 }), { mediaId: null, mediaCollectionId: 44 });
  assert.deepEqual(buildReviewTarget({ id: 91 }, null), { mediaId: 91, mediaCollectionId: null });
  assert.deepEqual(buildReviewTarget(null, null), { mediaId: null, mediaCollectionId: null });
});

test("series year ranges preserve missing, single-year and distinct-year output", () => {
  assert.equal(formatSeriesYearRange(null, null), null);
  assert.equal(formatSeriesYearRange(2001, null), "2001");
  assert.equal(formatSeriesYearRange(2001, 2001), "2001");
  assert.equal(formatSeriesYearRange(2001, 2007), "2001–2007");
  assert.equal(formatSeriesYearRange(null, 2020), null);
  assert.equal(formatSeriesYearRange(null, 2020, { includeEndOnly: true }), "–2020");
});

test("rank lookup keeps the full TV kind group denominator when displaying a subset", () => {
  const items = [
    review(1, "Episode", 10, { seriesId: 1 }),
    review(2, "Episode", 9, { seriesId: 2 }),
    review(3, "Episode", 8, { seriesId: 1 }),
    review(4, "Series", 10),
  ];
  const seriesOneEpisodes = items.filter((item) => item.kind === "Episode" && item.seriesId === 1);

  const episodeRanks = buildReviewRankLookups(items).get("TvShow:Episode");
  assert.equal(episodeRanks.totalCount, 3);
  assert.deepEqual(seriesOneEpisodes.map((item) => episodeRanks.rankById.get(item.id)), [1, 3]);
});
