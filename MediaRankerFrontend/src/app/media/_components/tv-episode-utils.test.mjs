import test from "node:test";
import assert from "node:assert/strict";
import { mergeEpisodePages, TV_EPISODE_PAGE_SIZE } from "./tv-episode-utils.mjs";

test("episode pages append in stable order, replace repeated IDs, and preserve inputs", () => {
  const first = [{ id: 1, title: "Pilot" }, { id: 2, title: "Two" }];
  const next = [{ id: 2, title: "Two updated" }, { id: 3, title: "Three" }];
  const merged = mergeEpisodePages(first, next);

  assert.deepEqual(merged, [
    { id: 1, title: "Pilot" },
    { id: 2, title: "Two updated" },
    { id: 3, title: "Three" },
  ]);
  assert.deepEqual(first, [{ id: 1, title: "Pilot" }, { id: 2, title: "Two" }]);
  assert.deepEqual(next, [{ id: 2, title: "Two updated" }, { id: 3, title: "Three" }]);
});

test("TV episode page size remains 25", () => {
  assert.equal(TV_EPISODE_PAGE_SIZE, 25);
});
