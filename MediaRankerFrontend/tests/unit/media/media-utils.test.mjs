import test from "node:test";
import assert from "node:assert/strict";
import {
  buildSeriesUpsertRequest,
  mergeEpisodePages,
  TV_EPISODE_PAGE_SIZE,
} from "../../../src/app/media/media-utils.ts";

test("series upsert uses the server's numeric Series enum and keeps the year", () => {
  assert.deepEqual(
    buildSeriesUpsertRequest({
      id: null,
      title: "  New Series  ",
      startYear: 2026,
    }),
    {
      id: null,
      title: "New Series",
      collectionType: 0,
      mediaType: "TvShow",
      parentMediaCollectionId: null,
      releaseDate: "2026-01-01",
    },
  );
});

test("episode pages append in stable order, replace repeated IDs, and preserve inputs", () => {
  const first = [
    { id: 1, title: "Pilot" },
    { id: 2, title: "Two" },
  ];
  const next = [
    { id: 2, title: "Two updated" },
    { id: 3, title: "Three" },
  ];
  const merged = mergeEpisodePages(first, next);

  assert.deepEqual(merged, [
    { id: 1, title: "Pilot" },
    { id: 2, title: "Two updated" },
    { id: 3, title: "Three" },
  ]);
  assert.deepEqual(first, [
    { id: 1, title: "Pilot" },
    { id: 2, title: "Two" },
  ]);
  assert.deepEqual(next, [
    { id: 2, title: "Two updated" },
    { id: 3, title: "Three" },
  ]);
});

test("TV episode page size remains 25", () => {
  assert.equal(TV_EPISODE_PAGE_SIZE, 25);
});
