import { expect, test } from "@playwright/test";
import { installApiMock, loginLocally, reviewFixture } from "../shared/api-fixtures";

test("failed review save keeps the draft and a successful retry creates one review", async ({ page }) => {
  const api = await installApiMock(page, {
    failFirstCreate: true,
    media: [{ id: 21, title: "Retryable Film", mediaType: "VideoGame" }],
  });
  await loginLocally(page);

  await page.getByRole("main").getByRole("button", { name: "New review" }).click();
  await page.getByRole("searchbox", { name: "Search the catalog" }).fill("Retryable");
  await page.getByRole("button", { name: /Retryable Film/ }).click();
  await expect(page.getByRole("button", { name: "Save review" })).toBeDisabled();
  await page.getByRole("group", { name: "Story" }).getByRole("button", { name: "7 out of 10" }).click();
  await page.getByRole("group", { name: "Craft" }).getByRole("button", { name: "8 out of 10" }).click();
  const notes = page.getByRole("textbox", { name: "Notes (optional)" });
  await notes.fill("Keep this draft after the first request fails.");

  await page.getByRole("button", { name: "Save review" }).click();
  await expect(page.getByRole("alert").filter({ hasText: "The review could not be saved. Try again." })).toBeVisible();
  await expect(notes).toHaveValue("Keep this draft after the first request fails.");
  await expect(page.getByRole("button", { name: "Save review" })).toBeEnabled();

  await page.getByRole("button", { name: "Save review" }).click();
  await expect(page.getByRole("dialog", { name: "New review" })).toBeHidden();
  await expect(page.getByRole("button", { name: /Open review for Retryable Film/ })).toHaveCount(1);
  expect(api.createAttempts).toBe(2);
  expect(api.reviews.get("VideoGame")).toHaveLength(1);
  expect(api.reviews.get("VideoGame")?.[0].notes).toBe("Keep this draft after the first request fails.");
  await api.assertNoUnexpectedRequests();
});

test("an acknowledged review edit survives a delayed stale library read", async ({ page }) => {
  const saved = reviewFixture({
    id: 31,
    mediaType: "Movie",
    mediaTitle: "Contended Film",
    notes: "Original notes from the first read.",
  });
  const api = await installApiMock(page, { reviews: [saved], lateSecondMovieRead: true });
  await loginLocally(page);

  await expect(page.getByRole("button", { name: /Open review for Contended Film/ })).toBeVisible();
  api.armLateRead();
  await page.getByRole("button", { name: /Open review for Contended Film/ }).click();
  await api.waitForLateRead();
  await expect(page.getByRole("heading", { name: "Contended Film" })).toBeVisible();
  await page.getByRole("button", { name: "Edit review" }).click();
  await page.getByRole("textbox", { name: "Notes (optional)" }).fill("Acknowledged edit from the user.");
  await page.getByRole("group", { name: "Story" }).getByRole("button", { name: "9 out of 10" }).click();
  await page.getByRole("group", { name: "Craft" }).getByRole("button", { name: "10 out of 10" }).click();
  await page.getByRole("button", { name: "Save review" }).click();
  const reviewDrawer = page.getByRole("dialog");
  await expect(reviewDrawer.getByText("Acknowledged edit from the user.", { exact: true })).toBeVisible();

  api.releaseLateRead();
  await api.waitForLateReadFinished();
  await api.waitForLateResponseConsumed();
  await expect(reviewDrawer.getByText("Acknowledged edit from the user.", { exact: true })).toBeVisible();
  await expect(reviewDrawer.getByText("Original notes from the first read.", { exact: true })).toHaveCount(0);
  await page.getByRole("button", { name: "Close review" }).click();
  const library = page.getByRole("main");
  await expect(library.getByRole("heading", { name: "Contended Film" })).toBeVisible();
  await expect(library.getByText("Acknowledged edit from the user.", { exact: true })).toBeVisible();
  await expect(library.getByText("Original notes from the first read.", { exact: true })).toHaveCount(0);
  const rankedReview = library.getByRole("button", { name: /Open review for Contended Film/ });
  await expect(rankedReview).toContainText("10");
  await api.assertNoUnexpectedRequests();
});
