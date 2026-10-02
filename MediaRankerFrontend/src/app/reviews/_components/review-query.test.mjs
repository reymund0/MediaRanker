import test from "node:test";
import assert from "node:assert/strict";
import { QueryClient, isCancelledError } from "@tanstack/react-query";
import {
  REVIEW_QUERY_ROOT,
  reviewQueryKey,
  reviewQueryOptions,
} from "./review-query.ts";

test("review options retain the existing route and cache identity", () => {
  const client = new QueryClient();
  try {
    const reviews = [{ id: 7, notes: "Existing review" }];
    client.setQueryData(["reviews", "VideoGame"], reviews);
    const options = reviewQueryOptions("VideoGame");
    assert.equal(options.route, "/api/reviews/byMediaType/VideoGame");
    assert.deepEqual(client.getQueryData(options.queryKey), reviews);
  } finally {
    client.clear();
  }
});

test("exact mutation cancellation prevents stale reads from replacing a saved review", async () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  try {
    const options = reviewQueryOptions("VideoGame");
    const saved = [{ id: 7, notes: "Saved edit" }];
    const otherType = [{ id: 8, notes: "Movie review" }];
    client.setQueryData(reviewQueryKey("Movie"), otherType);
    let completeRead;
    let readSignal;
    const read = client.fetchQuery({
      queryKey: options.queryKey,
      queryFn: ({ signal }) => {
        readSignal = signal;
        return new Promise((resolve) => { completeRead = resolve; });
      },
    }).catch((error) => error);

    await client.cancelQueries({ queryKey: reviewQueryKey("VideoGame"), exact: true });
    assert.equal(readSignal.aborted, true);
    client.setQueryData(reviewQueryKey("VideoGame"), saved);
    completeRead([{ id: 7, notes: "Stale server result" }]);
    assert.equal(isCancelledError(await read), true);
    assert.deepEqual(client.getQueryData(options.queryKey), saved);
    assert.deepEqual(client.getQueryData(reviewQueryKey("Movie")), otherType);
  } finally {
    client.clear();
  }
});

test("review root invalidates every category without invalidating the catalog", async () => {
  const client = new QueryClient();
  try {
    for (const type of ["VideoGame", "Movie", "TvShow", "Book", "Album", "Concert"]) {
      client.setQueryData(reviewQueryOptions(type).queryKey, []);
    }
    client.setQueryData(["media", "Movie"], []);
    await client.invalidateQueries({ queryKey: REVIEW_QUERY_ROOT, refetchType: "none" });
    for (const query of client.getQueryCache().findAll({ queryKey: REVIEW_QUERY_ROOT })) {
      assert.equal(query.state.isInvalidated, true);
    }
    assert.equal(client.getQueryState(["media", "Movie"]).isInvalidated, false);
  } finally {
    client.clear();
  }
});
