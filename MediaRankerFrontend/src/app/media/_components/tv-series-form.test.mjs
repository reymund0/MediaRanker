import test from "node:test";
import assert from "node:assert/strict";
import { buildSeriesUpsertRequest } from "./tv-series-form.mjs";

test("series upsert uses the server's numeric Series enum and keeps the year", () => {
  assert.deepEqual(buildSeriesUpsertRequest({ id: null, title: "  New Series  ", startYear: 2026 }), {
    id: null,
    title: "New Series",
    collectionType: 0,
    mediaType: "TvShow",
    parentMediaCollectionId: null,
    releaseDate: "2026-01-01",
  });
});
