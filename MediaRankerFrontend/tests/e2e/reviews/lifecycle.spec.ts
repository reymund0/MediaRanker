import { test, expect, signInAsLocalTestUser } from "../shared/fixtures";
import { gameTemplate } from "../templates/helpers";
import { reviewForTitle, scoreAllFields, startNewReviewFromCatalog } from "./helpers";
import { escapeRegExp } from "../shared/strings";

test("real review lifecycle persists create edit rank and delete", async ({
  e2eApi: api,
  page,
}) => {
  const template = await gameTemplate(api);
  const fields = [...template.fields].sort((a, b) => a.position - b.position);
  const reference = await api.createMedia("E2E lifecycle reference");
  const target = await api.createMedia("E2E lifecycle target");
  await api.createReview(
    reference,
    template,
    fields.map(() => 6),
    "Reference review",
  );

  await signInAsLocalTestUser(page);
  await startNewReviewFromCatalog(page, target.title);
  await scoreAllFields(
    page,
    fields,
    fields.map(() => 4),
  );
  await page
    .getByRole("textbox", { name: "Headline (optional)" })
    .fill("Created in the browser");
  await page
    .getByRole("textbox", { name: "Notes (optional)" })
    .fill("Saved notes survive a reload.");
  await page.getByRole("button", { name: "Save review" }).click();
  await expect(page.getByRole("dialog", { name: "New review" })).toBeHidden();
  await expect
    .poll(async () => (await reviewForTitle(api, target.title))?.overallScore)
    .toBe(4);

  // The parent harness uses this switch once to prove the fixture sweep catches
  // a review created by the UI, then reruns the same test normally.
  if (process.env.E2E_INJECT_POST_SAVE_FAILURE === "1") {
    throw new Error(
      "Intentional post-save failure for UI-created resource cleanup verification.",
    );
  }

  await page.goto("/reviews");
  await page.reload();
  const targetCard = page.getByRole("button", {
    name: new RegExp(`Open review for ${escapeRegExp(target.title)}`),
  });
  await expect(targetCard).toBeVisible();
  await targetCard.click();
  const reviewDetails = page.getByRole("dialog");
  await expect(
    reviewDetails.getByRole("heading", { name: target.title }),
  ).toBeVisible();
  await expect(
    reviewDetails.getByRole("heading", { name: "“Created in the browser”" }),
  ).toBeVisible();
  await expect(
    reviewDetails.getByText("Saved notes survive a reload.", { exact: true }),
  ).toBeVisible();
  await expect(
    reviewDetails.getByText("Overall score", { exact: true }),
  ).toBeVisible();
  await expect(
    reviewDetails.getByRole("button", { name: "Edit review" }),
  ).toBeVisible();
  expect((await reviewForTitle(api, target.title))?.reviewTitle).toBe(
    "Created in the browser",
  );

  await reviewDetails.getByRole("button", { name: "Edit review" }).click();
  await scoreAllFields(
    page,
    fields,
    fields.map(() => 9),
  );
  await reviewDetails
    .getByRole("textbox", { name: "Headline (optional)" })
    .fill("Moved to first place");
  await reviewDetails
    .getByRole("textbox", { name: "Notes (optional)" })
    .fill("Edited and saved.");
  await reviewDetails.getByRole("button", { name: "Save review" }).click();
  await expect(
    reviewDetails.getByRole("button", { name: "Close review" }),
  ).toBeVisible();
  await page.reload();

  const ranked = page.getByRole("button", {
    name: new RegExp(`Open review for ${escapeRegExp(target.title)}`),
  });
  await expect(ranked).toHaveAttribute(
    "aria-label",
    new RegExp("rank 1, score 9"),
  );
  await expect(
    page.getByRole("button", { name: /Video games, 2/ }),
  ).toBeVisible();
  await expect
    .poll(async () => (await reviewForTitle(api, target.title))?.overallScore)
    .toBe(9);
  let saved = await reviewForTitle(api, target.title);
  expect(saved?.reviewTitle).toBe("Moved to first place");
  expect(saved?.notes).toBe("Edited and saved.");
  expect(saved?.fields.map((field) => field.value)).toEqual(
    fields.map(() => 9),
  );

  await ranked.click();
  await reviewDetails.getByRole("button", { name: "Delete review" }).click();
  const confirmation = page.getByRole("dialog", { name: "Delete review" });
  await expect(confirmation).toContainText(target.title);
  await confirmation.getByRole("button", { name: "Cancel" }).click();
  await expect(confirmation).toBeHidden();
  expect(await reviewForTitle(api, target.title)).toBeTruthy();

  await reviewDetails.getByRole("button", { name: "Delete review" }).click();
  await page
    .getByRole("dialog", { name: "Delete review" })
    .getByRole("button", { name: "Delete review" })
    .click();
  await expect(page.getByRole("heading", { name: target.title })).toBeHidden();
  await page.reload();
  await expect(
    page.getByRole("button", {
      name: new RegExp(`Open review for ${escapeRegExp(target.title)}`),
    }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: /Video games, 1/ }),
  ).toBeVisible();
  saved = await reviewForTitle(api, target.title);
  expect(saved).toBeUndefined();
  const remaining = await api.get<Array<{ mediaTitle: string }>>(
    "/api/reviews/byMediaType/VideoGame",
  );
  expect(remaining.map((review) => review.mediaTitle)).toEqual([
    reference.title,
  ]);
});
