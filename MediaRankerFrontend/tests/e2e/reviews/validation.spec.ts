import { test, expect, signInAsLocalTestUser } from "../shared/fixtures";
import { gameTemplate } from "../templates/helpers";
import { reviewForTitle, scoreAllFields, startNewReviewFromCatalog } from "./helpers";
import { escapeRegExp } from "../shared/strings";

test("chooser validation midpoint cancellation and local session", async ({
  e2eApi: api,
  page,
}) => {
  const template = await gameTemplate(api);
  const fields = [...template.fields].sort((a, b) => a.position - b.position);
  const reviewed = await api.createMedia("E2E chooser already reviewed");
  const cancelTarget = await api.createMedia("E2E chooser cancelled title");
  const midpointTarget = await api.createMedia("E2E midpoint title");

  await signInAsLocalTestUser(page);
  const library = page.getByRole("main");
  await expect(
    library.getByRole("button", { name: "New review" }),
  ).toBeVisible();
  await page.reload();
  await expect(
    page.getByRole("heading", { name: "Your rankings" }),
  ).toBeVisible();
  // Seed the reviewed title after the empty Library has loaded. Opening the
  // chooser mounts its review-status query and checks the real API response.
  await api.createReview(
    reviewed,
    template,
    fields.map(() => 7),
    "Reviewed before chooser",
  );
  await library.getByRole("button", { name: "New review" }).click();
  const dialog = page.getByRole("dialog", { name: "New review" });
  const chooserSearch = dialog.getByRole("searchbox", {
    name: "Search the catalog",
  });
  await page.keyboard.press("Tab");
  await expect
    .poll(() =>
      dialog.evaluate((element) => element.contains(document.activeElement)),
    )
    .toBe(true);
  const chooserSearchIsFocused = () =>
    chooserSearch.evaluate((element) => element === document.activeElement);
  for (let tabCount = 0; tabCount < 10; tabCount++) {
    if (await chooserSearchIsFocused()) break;
    await page.keyboard.press("Tab");
  }
  await expect(chooserSearch).toBeFocused();
  await chooserSearch.fill(reviewed.title);
  const reviewedOption = dialog.getByRole("button", {
    name: new RegExp(`${escapeRegExp(reviewed.title)}.*Reviewed`),
  });
  await expect(reviewedOption).toBeDisabled();

  await chooserSearch.fill(cancelTarget.title);
  await dialog
    .getByRole("button", { name: new RegExp(escapeRegExp(cancelTarget.title)) })
    .click();
  await expect(
    dialog.getByText(
      "Picked automatically — it’s your only video game template.",
    ),
  ).toBeVisible();
  await dialog.getByRole("button", { name: "Change", exact: true }).click();
  await expect(chooserSearch).toHaveValue(cancelTarget.title);
  await dialog
    .getByRole("button", { name: new RegExp(escapeRegExp(cancelTarget.title)) })
    .click();
  const save = dialog.getByRole("button", { name: "Save review" });
  await scoreAllFields(
    page,
    fields.slice(0, -1),
    fields.slice(0, -1).map(() => 5),
  );
  await expect(save).toBeDisabled();
  await dialog.getByRole("button", { name: "Cancel" }).click();
  await expect(dialog).toBeHidden();
  expect(await reviewForTitle(api, cancelTarget.title)).toBeUndefined();

  await page.setViewportSize({ width: 390, height: 844 });
  await startNewReviewFromCatalog(page, midpointTarget.title);
  await scoreAllFields(page, fields, [8, 8, 9, 9]);
  await page
    .getByRole("textbox", { name: "Notes (optional)" })
    .fill("A deliberately long note. ".repeat(140));
  const reviewForm = page
    .getByRole("dialog", { name: "New review" })
    .locator("#new-review-score-form");
  await expect(
    reviewForm
      .getByText("Overall", { exact: true })
      .locator("xpath=following-sibling::*[1]"),
  ).toHaveText("8");
  const narrowSave = page.getByRole("button", { name: "Save review" });
  await expect(narrowSave).toBeEnabled();
  await expect(narrowSave).toBeInViewport();
  await narrowSave.click();
  await page.goto("/reviews");
  await page.reload();
  await expect
    .poll(
      async () =>
        (await reviewForTitle(api, midpointTarget.title))?.overallScore,
    )
    .toBe(8);
  const persisted = await reviewForTitle(api, midpointTarget.title);
  expect(
    persisted?.fields
      .slice()
      .sort(
        (left, right) =>
          left.templateFieldPosition - right.templateFieldPosition,
      )
      .map((field) => field.value),
  ).toEqual([8, 8, 9, 9]);

  await page.reload();
  await page
    .getByRole("button", {
      name: new RegExp(`Open review for ${escapeRegExp(midpointTarget.title)}`),
    })
    .click();
  await page.getByRole("button", { name: "Edit review" }).click();
  await page
    .getByRole("group", { name: fields[0].name })
    .getByRole("button", { name: "10 out of 10" })
    .click();
  await page.getByRole("button", { name: "Cancel" }).click();
  await page.reload();
  const afterCancel = await reviewForTitle(api, midpointTarget.title);
  expect(
    afterCancel?.fields
      .slice()
      .sort(
        (left, right) =>
          left.templateFieldPosition - right.templateFieldPosition,
      )
      .map((field) => field.value),
  ).toEqual([8, 8, 9, 9]);

  await page.getByRole("button", { name: "Open account menu" }).click();
  await page.getByRole("menuitem", { name: "Sign out" }).click();
  await expect(page).toHaveURL(/\/auth\/login$/);
  await expect(
    page.getByRole("heading", { name: "Welcome back" }),
  ).toBeVisible();
  await expect(
    page.getByText("Sign in to your library.", { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByText("A deliberately long note.", { exact: false }),
  ).toHaveCount(0);
});
