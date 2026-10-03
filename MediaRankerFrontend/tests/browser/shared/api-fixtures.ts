import type { Page, Route } from "@playwright/test";
import type { ReviewDto, ReviewInsertRequest, ReviewUpdateRequest, ReviewFieldDto } from "../../../src/app/reviews/contracts";
import type { MediaDto } from "../../../src/app/media/contracts";
import type { MediaType, TemplateDto } from "../../../src/lib/contracts/shared";
import { roundToEven } from "../../../src/app/reviews/_components/review-utils";

declare global {
  interface Window {
    __lateMockReviewJsonParsed?: boolean;
  }
}

const API_ORIGIN = process.env.E2E_API_URL ?? "http://127.0.0.1:3999";
const LOCAL_IMAGE = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/pN8AAAAASUVORK5CYII=",
  "base64",
);

export type ReviewField = ReviewFieldDto;

export type Review = ReviewDto;

export type MockOptions = {
  reviews?: Review[];
  media?: MockMedia[];
  failFirstCreate?: boolean;
  lateSecondMovieRead?: boolean;
  transitionPendingCover?: boolean;
};

export type MockMedia = Pick<MediaDto, "id" | "title" | "mediaType"> &
  Partial<Pick<MediaDto, "releaseDate" | "coverImageUrl" | "coverStatus">>;

export function createMockPageResult<T>(items: T[], url: URL) {
  const page = Number(url.searchParams.get("page") ?? 0);
  const pageSize = Number(url.searchParams.get("pageSize") ?? 25);
  const start = page * pageSize;

  return {
    items: items.slice(start, start + pageSize),
    totalCount: url.searchParams.get("includeTotalCount") === "true" ? items.length : null,
    page,
    pageSize,
  };
}

const template = (mediaType: string): TemplateDto => ({
  id: 10,
  isSystem: false,
  userId: "local-test-user",
  name: "Core template",
  description: null,
  createdAt: "2025-01-01T00:00:00Z",
  updatedAt: "2025-01-01T00:00:00Z",
  mediaType: mediaType as MediaType,
  fields: [
    { id: 101, name: "Story", position: 0 },
    { id: 102, name: "Craft", position: 1 },
  ],
});

export function reviewFixture(overrides: Partial<Review> = {}): Review {
  const id = overrides.id ?? 1;
  const mediaType = overrides.mediaType ?? "Movie";
  const fields = overrides.fields ?? [
    { reviewId: id, templateFieldId: 101, templateFieldName: "Story", templateFieldPosition: 0, value: 7 },
    { reviewId: id, templateFieldId: 102, templateFieldName: "Craft", templateFieldPosition: 1, value: 8 },
  ];
  return {
    id,
    userId: "local-test-user",
    overallScore: 8,
    reviewTitle: null,
    notes: null,
    consumedAt: null,
    createdAt: "2025-01-01T00:00:00Z",
    updatedAt: "2025-01-01T00:00:00Z",
    fields,
    templateId: 10,
    templateName: "Core template",
    mediaId: id + 100,
    mediaTitle: `Review title ${id}`,
    mediaType,
    mediaReleaseDate: "2024-01-01",
    mediaCoverImageUrl: null,
    coverStatus: "unsupported",
    kind: "Title",
    mediaCollectionId: null,
    seriesId: null,
    seriesTitle: null,
    seasonNumber: null,
    episodeNumber: null,
    seriesStartYear: null,
    seriesEndYear: null,
    seasonCount: null,
    episodeCount: null,
    ...overrides,
  };
}

function makeDeferred() {
  let resolve!: () => void;
  const promise = new Promise<void>((done) => { resolve = done; });
  return { promise, resolve };
}

export async function installApiMock(page: Page, options: MockOptions = {}) {
  const reviews = new Map<string, Review[]>();
  for (const mediaType of ["VideoGame", "Movie", "TvShow", "Book", "Album", "Concert"]) {
    reviews.set(mediaType, []);
  }
  for (const item of options.reviews ?? []) {
    reviews.get(item.mediaType)?.push(item);
  }

  const media = options.media ?? [];
  const templates = new Map<string, ReturnType<typeof template>>();
  let createAttempts = 0;
  let nextReviewId = 500;
  let lateReadArmed = false;
  let lateReadCaptured = false;
  let pendingCoverRefreshAllowed = false;
  let shouldFailCreate = options.failFirstCreate ?? false;
  const lateRead = makeDeferred();
  const releaseLateRead = makeDeferred();
  const lateReadFinished = makeDeferred();
  const unexpectedRequests: string[] = [];
  const nonLoopbackRequests: string[] = [];
  const imageRequests: string[] = [];

  if (options.lateSecondMovieRead) {
    await page.addInitScript(() => {
      const nativeJson = Response.prototype.json;
      Response.prototype.json = async function () {
        const value = await nativeJson.call(this);
        if (this.headers.get("x-test-late-review-response") === "consumed") {
          window.__lateMockReviewJsonParsed = true;
        }
        return value;
      };
    });
  }

  page.on("request", (request) => {
    const url = new URL(request.url());
    if (!(["localhost", "127.0.0.1", "[::1]", "::1"].includes(url.hostname))) {
      nonLoopbackRequests.push(request.url());
    }
  });

  const json = (route: Route, body: unknown, status = 200, headers?: Record<string, string>) => route.fulfill({
    status,
    contentType: "application/json",
    headers,
    body: JSON.stringify(body),
  });

  await page.route(`${API_ORIGIN}/**`, async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;
    const method = request.method();

    if (path.startsWith("/image/")) {
      imageRequests.push(path);
      if (path.endsWith("/broken.png")) {
        await route.fulfill({ status: 404, body: "not found" });
      } else if (path.endsWith("/ready.png")) {
        await route.fulfill({ status: 200, contentType: "image/png", body: LOCAL_IMAGE });
      } else {
        unexpectedRequests.push(`IMAGE ${path}`);
        await route.fulfill({ status: 404, body: "unexpected image request" });
      }
      return;
    }

    const reviewMatch = path.match(/^\/api\/reviews\/byMediaType\/([^/]+)$/);
    if (method === "GET" && reviewMatch) {
      const mediaType = decodeURIComponent(reviewMatch[1]);
      if (options.lateSecondMovieRead && mediaType === "Movie" && lateReadArmed && !lateReadCaptured) {
        lateReadCaptured = true;
          const staleReviews = [...(reviews.get(mediaType) ?? [])];
          lateRead.resolve();
          await releaseLateRead.promise;
          await json(route, staleReviews, 200, {
            "x-test-late-review-response": "consumed",
            "access-control-expose-headers": "x-test-late-review-response",
          });
          lateReadFinished.resolve();
          return;
      }
      if (options.transitionPendingCover) {
        const typeReviews = reviews.get(mediaType) ?? [];
        if (pendingCoverRefreshAllowed) {
          for (const review of typeReviews) {
            if (review.coverStatus === "pending") {
              review.coverStatus = "ready";
              review.mediaCoverImageUrl = `${API_ORIGIN}/image/ready.png`;
            }
          }
        }
      }
      await json(route, reviews.get(mediaType) ?? []);
      return;
    }

    if (method === "GET" && path === "/api/media") {
      const mediaType = url.searchParams.get("mediaType") ?? "VideoGame";
      const search = url.searchParams.get("searchTerm")?.toLocaleLowerCase() ?? "";
      const matches = media
        .filter((item) => item.mediaType === mediaType)
        .filter((item) => !search || item.title.toLocaleLowerCase().includes(search))
        .sort((left, right) => left.title.localeCompare(right.title));
      const pageItems = matches.map((item) => ({
        id: item.id,
        title: item.title,
        releaseDate: item.releaseDate ?? "2024-01-01",
        createdAt: "2025-01-01T00:00:00Z",
        updatedAt: "2025-01-01T00:00:00Z",
        mediaType: item.mediaType,
        coverImageUrl: item.coverImageUrl ?? null,
        coverStatus: item.coverStatus ?? "unsupported",
      }));
      await json(route, createMockPageResult(pageItems, url));
      return;
    }

    if (method === "GET" && path === "/api/media/showcase") {
      await json(route, []);
      return;
    }

    if (method === "GET" && path === "/api/MediaCollection") {
      await json(route, createMockPageResult([], url));
      return;
    }

    const typeTemplateMatch = path.match(/^\/api\/templates\/([^/]+)$/);
    if (method === "GET" && typeTemplateMatch) {
      const mediaType = decodeURIComponent(typeTemplateMatch[1]);
      const item = templates.get(mediaType) ?? template(mediaType);
      templates.set(mediaType, item);
      await json(route, [item]);
      return;
    }

    if (method === "GET" && path === "/api/templates") {
      await json(route, [...templates.values()]);
      return;
    }

    if (method === "POST" && path === "/api/reviews") {
      createAttempts += 1;
      if (shouldFailCreate) {
        shouldFailCreate = false;
        await json(route, {
          type: "about:blank",
          title: "Save failed",
          status: 503,
          detail: "The review could not be saved. Try again.",
        }, 503);
        return;
      }
      const body = request.postDataJSON() as ReviewInsertRequest;
      const target = media.find((item) => item.id === body.mediaId);
      const saved = reviewFixture({
        id: nextReviewId++,
        mediaId: body.mediaId,
        mediaTitle: target?.title ?? "Saved title",
        mediaType: target?.mediaType ?? "VideoGame",
        mediaReleaseDate: target?.releaseDate ?? "2024-01-01",
        mediaCoverImageUrl: target?.coverImageUrl ?? null,
        coverStatus: target?.coverStatus ?? "unsupported",
        reviewTitle: body.reviewTitle,
        notes: body.notes,
        overallScore: roundToEven(body.fields.reduce((sum, field) => sum + field.value, 0) / body.fields.length),
        fields: body.fields.map((field, position) => ({
          reviewId: nextReviewId - 1,
          templateFieldId: field.templateFieldId,
          templateFieldName: field.templateFieldId === 101 ? "Story" : "Craft",
          templateFieldPosition: position,
          value: field.value,
        })),
      });
      const typeReviews = reviews.get(saved.mediaType) ?? [];
      typeReviews.unshift(saved);
      reviews.set(saved.mediaType, typeReviews);
      await json(route, saved);
      return;
    }

    if (method === "PATCH" && path === "/api/reviews/update") {
      const body = request.postDataJSON() as ReviewUpdateRequest;
      const existing = [...reviews.values()].flat().find((item) => item.id === body.id);
      if (!existing) {
        await json(route, { title: "Review not found", status: 404 }, 404);
        return;
      }
      const saved: Review = {
        ...existing,
        reviewTitle: body.reviewTitle,
        notes: body.notes,
        updatedAt: "2025-01-02T00:00:00Z",
        overallScore: roundToEven(body.fields.reduce((sum, field) => sum + field.value, 0) / body.fields.length),
        fields: body.fields.map((field, position) => ({
          reviewId: body.id,
          templateFieldId: field.templateFieldId,
          templateFieldName: field.templateFieldId === 101 ? "Story" : "Craft",
          templateFieldPosition: position,
          value: field.value,
        })),
      };
      const typeReviews = reviews.get(saved.mediaType) ?? [];
      reviews.set(saved.mediaType, typeReviews.map((item) => item.id === saved.id ? saved : item));
      await json(route, saved);
      return;
    }

    if (method === "DELETE" && /^\/api\/reviews\/\d+$/.test(path)) {
      const id = Number(path.split("/").at(-1));
      for (const [mediaType, values] of reviews) {
        reviews.set(mediaType, values.filter((item) => item.id !== id));
      }
      await route.fulfill({ status: 204, body: "" });
      return;
    }

    unexpectedRequests.push(`${method} ${path}${url.search}`);
    await json(route, {
      type: "about:blank",
      title: "Unexpected mocked API request",
      status: 501,
      detail: `${method} ${path}${url.search} is not configured in this browser test.`,
    }, 501);
  });

  return {
    reviews,
    unexpectedRequests,
    nonLoopbackRequests,
    imageRequests,
    get createAttempts() { return createAttempts; },
    waitForLateRead: () => lateRead.promise,
    armLateRead() { lateReadArmed = true; },
    releaseLateRead: () => releaseLateRead.resolve(),
    waitForLateReadFinished: () => lateReadFinished.promise,
    async waitForLateResponseConsumed() {
      await page.waitForFunction(() => window.__lateMockReviewJsonParsed === true);
      await page.evaluate(() => new Promise<void>((resolve) => {
        requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
      }));
    },
    allowPendingCoverRefresh() { pendingCoverRefreshAllowed = true; },
    async assertNoUnexpectedRequests() {
      if (unexpectedRequests.length) throw new Error(`Unexpected API requests: ${unexpectedRequests.join(", ")}`);
      if (nonLoopbackRequests.length) throw new Error(`Unexpected non-loopback requests: ${nonLoopbackRequests.join(", ")}`);
    },
  };
}

export async function loginLocally(page: Page) {
  await page.goto("/auth/login");
  await page.getByRole("button", { name: "Use local test user" }).click();
  await page.waitForURL("**/reviews");
  await page.getByRole("heading", { name: "Your rankings" }).waitFor();
}
