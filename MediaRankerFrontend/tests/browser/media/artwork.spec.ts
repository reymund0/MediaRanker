import { expect, test } from "@playwright/test";
import { installApiMock, loginLocally, reviewFixture } from "../shared/api-fixtures";

test("pending artwork refreshes to a fulfilled local image and missing artwork has a fallback", async ({ page }) => {
  await page.clock.install({ time: new Date("2025-01-01T00:00:00Z") });
  const pending = reviewFixture({
    id: 41,
    mediaType: "VideoGame",
    mediaTitle: "Resolving Cover",
    coverStatus: "pending",
  });
  const missing = reviewFixture({
    id: 42,
    mediaType: "VideoGame",
    mediaTitle: "No Cover",
    coverStatus: "missing",
  });
  const api = await installApiMock(page, {
    reviews: [pending, missing],
    transitionPendingCover: true,
  });
  await loginLocally(page);

  const resolvingTile = page.getByRole("button", { name: /Open review for Resolving Cover/ });
  await expect(resolvingTile.getByRole("img", { name: "Resolving Cover cover art is being found" })).toBeVisible();
  api.allowPendingCoverRefresh();
  await page.clock.fastForward(2_000);
  const readyImage = resolvingTile.getByRole("img", { name: "Resolving Cover cover art" });
  await expect(readyImage).toBeVisible();
  await expect.poll(() => readyImage.evaluate((image) => (image as HTMLImageElement).complete)).toBe(true);
  await expect.poll(() => readyImage.evaluate((image) => (image as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);
  const missingTile = page.getByRole("button", { name: /Open review for No Cover/ });
  await expect(missingTile.getByRole("img", { name: "No Cover cover unavailable" })).toBeVisible();
  expect(api.imageRequests).toContain("/image/ready.png");
  await api.assertNoUnexpectedRequests();
});

test("failed image loads and failed artwork status both retain a usable fallback", async ({ page }) => {
  const failedLoad = reviewFixture({
    id: 51,
    mediaType: "VideoGame",
    mediaTitle: "Broken Cover Image",
    coverStatus: "ready",
    mediaCoverImageUrl: `${process.env.E2E_API_URL ?? "http://127.0.0.1:3999"}/image/broken.png`,
  });
  const failedStatus = reviewFixture({
    id: 52,
    mediaType: "VideoGame",
    mediaTitle: "Provider Failure",
    coverStatus: "failed",
  });
  const api = await installApiMock(page, { reviews: [failedLoad, failedStatus] });
  await loginLocally(page);

  const brokenTile = page.getByRole("button", { name: /Open review for Broken Cover Image/ });
  const failedTile = page.getByRole("button", { name: /Open review for Provider Failure/ });
  await expect(failedTile.getByRole("img", { name: "Provider Failure cover unavailable" })).toBeVisible();
  await expect(brokenTile.getByRole("img", { name: "Broken Cover Image cover art cover unavailable" })).toBeVisible();
  expect(api.imageRequests).toContain("/image/broken.png");
  await api.assertNoUnexpectedRequests();
});
