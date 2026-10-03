import {
  expect,
  request as playwrightRequest,
  test as base,
} from "@playwright/test";
import type { APIRequestContext, Page } from "@playwright/test";
import { access, appendFile, mkdir, writeFile } from "node:fs/promises";
import path from "node:path";

export const MEDIA_TYPES = [
  "VideoGame",
  "Movie",
  "TvShow",
  "Book",
  "Album",
  "Concert",
] as const;
export type MediaType = (typeof MEDIA_TYPES)[number];

export interface MediaDto {
  id: number;
  title: string;
  mediaType: MediaType;
  releaseDate: string | null;
}

export interface TemplateFieldDto {
  id: number;
  name: string;
  position: number;
}

export interface TemplateDto {
  id: number;
  isSystem: boolean;
  name: string;
  mediaType: MediaType;
  fields: TemplateFieldDto[];
}

export interface ReviewDto {
  id: number;
  mediaId: number | null;
  mediaTitle: string;
  mediaType: MediaType;
  overallScore: number;
  reviewTitle: string | null;
  notes: string | null;
  templateId: number;
  fields: Array<{
    templateFieldId: number;
    templateFieldName: string;
    templateFieldPosition: number;
    value: number;
  }>;
}

type Snapshot = {
  media: Set<number>;
  templates: Set<number>;
  reviews: Set<number>;
};

const API_TOKEN = "MediaRankerLocalTest";
const RUN_DIR = process.env.E2E_RUN_DIR;

function requiredEnvironment(name: string, value: string | undefined): string {
  if (!value) throw new Error(`${name} must be set by the E2E harness.`);
  return value;
}

export class E2EApi {
  readonly apiUrl: string;
  readonly runDir: string;
  readonly context: APIRequestContext;
  private readonly createdMediaIds = new Set<number>();
  private readonly createdTemplateIds = new Set<number>();
  private readonly createdReviewIds = new Set<number>();

  private constructor(
    apiUrl: string,
    runDir: string,
    context: APIRequestContext,
  ) {
    this.apiUrl = apiUrl;
    this.runDir = runDir;
    this.context = context;
  }

  static async create(): Promise<E2EApi> {
    const apiUrl = requiredEnvironment("E2E_API_URL", process.env.E2E_API_URL);
    const runDir = path.resolve(requiredEnvironment("E2E_RUN_DIR", RUN_DIR));
    const context = await playwrightRequest.newContext({
      extraHTTPHeaders: { Authorization: `Bearer ${API_TOKEN}` },
      timeout: 15_000,
    });
    const api = new E2EApi(apiUrl, runDir, context);
    try {
      await api.assertNotPoisoned();
    } catch (error) {
      await api.dispose();
      throw error;
    }
    return api;
  }

  async dispose(): Promise<void> {
    await this.context.dispose();
  }

  async get<T>(route: string): Promise<T> {
    return this.call<T>("GET", route);
  }

  async post<T>(route: string, body: unknown): Promise<T> {
    return this.call<T>("POST", route, body);
  }

  async delete(route: string): Promise<void> {
    await this.call<unknown>("DELETE", route);
  }

  async createMedia(
    title: string,
    mediaType: MediaType = "VideoGame",
  ): Promise<MediaDto> {
    const media = await this.post<MediaDto>("/api/media", {
      id: null,
      title,
      mediaType,
      releaseDate: "2020-01-02",
    });
    this.createdMediaIds.add(media.id);
    return media;
  }

  async createTemplate(
    name: string,
    mediaType: MediaType,
    fields: string[],
  ): Promise<TemplateDto> {
    const template = await this.post<TemplateDto>("/api/templates", {
      id: null,
      mediaType,
      name,
      description: "Playwright E2E fixture",
      fields: fields.map((fieldName, position) => ({
        id: null,
        name: fieldName,
        position,
      })),
    });
    this.createdTemplateIds.add(template.id);
    return template;
  }

  async createReview(
    media: MediaDto,
    template: TemplateDto,
    values: number[],
    reviewTitle: string,
  ): Promise<ReviewDto> {
    const review = await this.post<ReviewDto>("/api/reviews", {
      mediaId: media.id,
      mediaCollectionId: null,
      templateId: template.id,
      reviewTitle,
      notes: "API-created fixture review",
      consumedAt: null,
      fields: [...template.fields]
        .sort((left, right) => left.position - right.position)
        .map((field, index) => ({
          templateFieldId: field.id,
          value: values[index] ?? values[0],
        })),
    });
    this.createdReviewIds.add(review.id);
    return review;
  }

  async snapshot(): Promise<Snapshot> {
    const [media, templates, reviews] = await Promise.all([
      this.allMedia(),
      this.get<TemplateDto[]>("/api/templates"),
      this.allReviews(),
    ]);
    return {
      media: new Set(media.map((item) => item.id)),
      templates: new Set(templates.map((item) => item.id)),
      reviews: new Set(reviews.map((item) => item.id)),
    };
  }

  async assertCleanStartingState(): Promise<void> {
    const [templates, reviews] = await Promise.all([
      this.get<TemplateDto[]>("/api/templates"),
      this.allReviews(),
    ]);
    expect(
      reviews,
      "the local E2E user must start with no reviews",
    ).toHaveLength(0);
    expect(
      templates.filter((template) => !template.isSystem),
      "the local E2E user must start with no custom templates",
    ).toHaveLength(0);
  }

  async cleanupSince(baseline: Snapshot): Promise<void> {
    const failures: string[] = [];
    const attempted = {
      reviews: [] as number[],
      templates: [] as number[],
      media: [] as number[],
    };
    const [reviews, templates, media] = await Promise.all([
      this.reviewsForCleanup(failures),
      this.get<TemplateDto[]>("/api/templates").catch((error: unknown) => {
        failures.push(
          `Could not list templates during cleanup: ${errorMessage(error)}`,
        );
        return [];
      }),
      this.mediaForCleanup(failures),
    ]);

    // Resource discovery and deletion continue independently, so one failure
    // never prevents cleanup attempts for other kinds.
    const reviewIds = new Set([
      ...this.createdReviewIds,
      ...reviews.map((review) => review.id),
    ]);
    for (const id of difference(reviewIds, baseline.reviews)) {
      attempted.reviews.push(id);
      await this.tryDelete(`/api/reviews/${id}`, `review ${id}`, failures);
    }

    const customTemplateIds = new Set(this.createdTemplateIds);
    for (const template of templates) {
      if (!template.isSystem) customTemplateIds.add(template.id);
    }
    for (const id of difference(customTemplateIds, baseline.templates)) {
      attempted.templates.push(id);
      await this.tryDelete(`/api/templates/${id}`, `template ${id}`, failures);
    }

    const mediaIds = new Set([
      ...this.createdMediaIds,
      ...media.map((item) => item.id),
    ]);
    for (const id of difference(mediaIds, baseline.media)) {
      attempted.media.push(id);
      await this.tryDelete(`/api/media/${id}`, `media ${id}`, failures);
    }

    const [verifiedReviews, verifiedTemplates, verifiedMedia] =
      await Promise.all([
        this.allReviews().catch((error: unknown) => {
          failures.push(
            `Could not verify reviews after cleanup: ${errorMessage(error)}`,
          );
          return undefined;
        }),
        this.get<TemplateDto[]>("/api/templates").catch((error: unknown) => {
          failures.push(
            `Could not verify templates after cleanup: ${errorMessage(error)}`,
          );
          return undefined;
        }),
        this.allMedia().catch((error: unknown) => {
          failures.push(
            `Could not verify media after cleanup: ${errorMessage(error)}`,
          );
          return undefined;
        }),
      ]);
    const verified: Array<[string, Set<number> | undefined, Set<number>]> = [
      [
        "media",
        verifiedMedia && new Set(verifiedMedia.map((item) => item.id)),
        baseline.media,
      ],
      [
        "templates",
        verifiedTemplates && new Set(verifiedTemplates.map((item) => item.id)),
        baseline.templates,
      ],
      [
        "reviews",
        verifiedReviews && new Set(verifiedReviews.map((item) => item.id)),
        baseline.reviews,
      ],
    ];
    for (const [kind, actual, expected] of verified) {
      if (!actual) continue;
      for (const id of difference(actual, expected))
        failures.push(`New ${kind} ${id} remained after cleanup.`);
      for (const id of difference(expected, actual))
        failures.push(`Baseline ${kind} ${id} disappeared during cleanup.`);
    }

    const baselineRestored = verified.every(
      ([, actual, expected]) =>
        actual !== undefined &&
        actual.size === expected.size &&
        difference(actual, expected).length === 0 &&
        difference(expected, actual).length === 0,
    );
    const cleanupRecord = {
      recordedAt: new Date().toISOString(),
      attemptedDeletes: attempted,
      baselineCounts: {
        media: baseline.media.size,
        templates: baseline.templates.size,
        reviews: baseline.reviews.size,
      },
      baselineRestored: baselineRestored && failures.length === 0,
      failureCount: failures.length,
    };
    try {
      await mkdir(this.runDir, { recursive: true });
      await appendFile(
        path.join(this.runDir, "fixture-cleanup.jsonl"),
        `${JSON.stringify(cleanupRecord)}\n`,
        "utf8",
      );
    } catch (error) {
      failures.push(
        `Could not record fixture cleanup evidence: ${errorMessage(error)}`,
      );
    }

    if (failures.length) {
      const markerPath = path.join(this.runDir, "fixture-cleanup-failed.json");
      await mkdir(this.runDir, { recursive: true });
      await writeFile(
        markerPath,
        JSON.stringify(
          { failures, recordedAt: new Date().toISOString() },
          null,
          2,
        ),
        "utf8",
      );
      throw new Error(
        `Fixture cleanup failed; this E2E run is poisoned. See ${markerPath}:\n${failures.join("\n")}`,
      );
    }
  }

  private async allMedia(): Promise<MediaDto[]> {
    const items: MediaDto[] = [];
    for (const mediaType of MEDIA_TYPES) {
      let page = 0;
      while (true) {
        const result = await this.get<{ items: MediaDto[]; pageSize: number }>(
          `/api/media?mediaType=${mediaType}&page=${page}&pageSize=100`,
        );
        items.push(...result.items);
        if (result.items.length < result.pageSize) break;
        page++;
      }
    }
    return items;
  }

  private async allReviews(): Promise<ReviewDto[]> {
    const groups = await Promise.all(
      MEDIA_TYPES.map((mediaType) =>
        this.get<ReviewDto[]>(`/api/reviews/byMediaType/${mediaType}`),
      ),
    );
    return groups.flat();
  }

  private async reviewsForCleanup(failures: string[]): Promise<ReviewDto[]> {
    const reviews: ReviewDto[] = [];
    for (const mediaType of MEDIA_TYPES) {
      try {
        reviews.push(
          ...(await this.get<ReviewDto[]>(
            `/api/reviews/byMediaType/${mediaType}`,
          )),
        );
      } catch (error) {
        failures.push(
          `Could not list ${mediaType} reviews during cleanup: ${errorMessage(error)}`,
        );
      }
    }
    return reviews;
  }

  private async mediaForCleanup(failures: string[]): Promise<MediaDto[]> {
    const items: MediaDto[] = [];
    for (const mediaType of MEDIA_TYPES) {
      let page = 0;
      while (true) {
        try {
          const result = await this.get<{
            items: MediaDto[];
            pageSize: number;
          }>(`/api/media?mediaType=${mediaType}&page=${page}&pageSize=100`);
          items.push(...result.items);
          if (result.items.length < result.pageSize) break;
          page++;
        } catch (error) {
          failures.push(
            `Could not list ${mediaType} media page ${page} during cleanup: ${errorMessage(error)}`,
          );
          break;
        }
      }
    }
    return items;
  }

  private async call<T>(
    method: string,
    route: string,
    body?: unknown,
  ): Promise<T> {
    const response = await this.context.fetch(
      new URL(route, this.apiUrl).toString(),
      {
        method,
        ...(body === undefined ? {} : { data: body }),
      },
    );
    if (!response.ok()) {
      const details = await response.text().catch(() => "<body unavailable>");
      throw new Error(
        `${method} ${route} returned ${response.status()}: ${details}`,
      );
    }
    if (response.status() === 204) return undefined as T;
    const text = await response.text();
    return text ? (JSON.parse(text) as T) : (undefined as T);
  }

  private async tryDelete(
    route: string,
    resource: string,
    failures: string[],
  ): Promise<void> {
    try {
      await this.delete(route);
    } catch (error) {
      failures.push(`Could not delete ${resource}: ${errorMessage(error)}`);
    }
  }

  private async assertNotPoisoned(): Promise<void> {
    const markerPath = path.join(this.runDir, "fixture-cleanup-failed.json");
    try {
      await access(markerPath);
      throw new Error(
        `E2E run is poisoned by a previous fixture cleanup failure: ${markerPath}`,
      );
    } catch (error) {
      if (isNotFound(error)) return;
      throw error;
    }
  }
}

function isNotFound(error: unknown): boolean {
  return (
    typeof error === "object" &&
    error !== null &&
    "code" in error &&
    error.code === "ENOENT"
  );
}

function difference(left: Set<number>, right: Set<number>): number[] {
  return [...left].filter((value) => !right.has(value));
}

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

export async function signInAsLocalTestUser(page: Page): Promise<void> {
  await page.goto("/auth/login");
  await page.getByRole("button", { name: "Use local test user" }).click();
  await expect(page).toHaveURL(/\/reviews$/);
  await expect(
    page.getByRole("heading", { name: "Your rankings" }),
  ).toBeVisible();
}

export const test = base.extend<{ e2eApi: E2EApi; networkBoundary: void }>({
  e2eApi: async ({}, provide) => {
    const api = await E2EApi.create();
    let baseline: Snapshot | undefined;
    try {
      baseline = await api.snapshot();
      await api.assertCleanStartingState();
      await provide(api);
    } finally {
      try {
        if (baseline) await api.cleanupSince(baseline);
      } finally {
        await api.dispose();
      }
    }
  },
  networkBoundary: [
    async ({ page }, provide) => {
      const externalRequests: string[] = [];
      page.on("request", (request) => {
        const url = new URL(request.url());
        if (
          !["localhost", "127.0.0.1", "::1", "[::1]"].includes(url.hostname)
        ) {
          externalRequests.push(`${url.origin}${url.pathname}`);
        }
      });
      await provide();
      expect(
        externalRequests,
        "real E2E must not contact non-loopback hosts",
      ).toEqual([]);
    },
    { auto: true },
  ],
});

export { expect };
