import { test, expect, signInAsLocalTestUser } from "../shared/fixtures";
import type { TemplateDto } from "../shared/fixtures";
import { gameTemplate } from "./helpers";
import { reviewForTitle, scoreAllFields, startNewReviewFromCatalog } from "../reviews/helpers";
import { escapeRegExp } from "../shared/strings";

test("built-in template duplicates reorders persists and drives review field order", async ({
  e2eApi: api,
  page,
}) => {
  const builtIn = await gameTemplate(api);
  const originalNames = [...builtIn.fields]
    .sort((a, b) => a.position - b.position)
    .map((field) => field.name);
  const media = await api.createMedia("E2E reordered template title");
  await signInAsLocalTestUser(page);
  await page.goto("/templates");
  const builtInCard = page
    .getByText(builtIn.name, { exact: true })
    .locator("xpath=../..");
  await expect(builtInCard).toContainText("Built-in templates are read-only.");
  await expect(builtInCard.getByRole("button", { name: "Edit" })).toHaveCount(
    0,
  );
  await builtInCard.getByRole("button", { name: "Duplicate" }).click();

  const name = "E2E reordered game template";
  await page.getByRole("textbox", { name: "Name" }).fill(name);
  const reorderFirst = page.getByRole("button", { name: "Reorder score 1" });
  await reorderFirst.focus();
  await expect(reorderFirst).toBeFocused();
  await page.keyboard.press("Space");
  const dragAnnouncement = page.getByRole("status");
  await expect(reorderFirst).toHaveAttribute("aria-pressed", "true");
  await page.keyboard.press("ArrowDown");
  await expect
    .poll(async () => {
      const announcement = await dragAnnouncement.textContent();
      const match = announcement?.match(
        /^Draggable item (.+) was moved over droppable area (.+)\.$/,
      );
      return match ? match[1] !== match[2] : false;
    })
    .toBe(true);
  await page.keyboard.press("Space");
  const reorderedNames = [
    originalNames[1],
    originalNames[0],
    ...originalNames.slice(2),
  ];
  for (let index = 0; index < reorderedNames.length; index++) {
    await expect(
      page.getByRole("textbox", { name: `Score ${index + 1}` }),
    ).toHaveValue(reorderedNames[index]);
  }
  await page.getByRole("button", { name: "Save template" }).click();
  await expect(page.getByText(name, { exact: true })).toBeVisible();
  await page.reload();

  const templates = await api.get<TemplateDto[]>("/api/templates/VideoGame");
  const duplicate = templates.find((candidate) => candidate.name === name);
  expect(duplicate).toBeTruthy();
  const fields = [...duplicate!.fields].sort((a, b) => a.position - b.position);
  expect(fields.map((field) => field.name)).toEqual(reorderedNames);

  await startNewReviewFromCatalog(page, media.title);
  await page.getByRole("combobox", { name: "Template" }).click();
  await page.getByRole("option", { name: name, exact: true }).click();
  await scoreAllFields(
    page,
    fields,
    fields.map((_, index) => index + 3),
  );
  await page.getByRole("button", { name: "Save review" }).click();
  await expect.poll(async () => reviewForTitle(api, media.title)).toBeTruthy();
  await page.goto("/reviews");
  await page.reload();
  await page
    .getByRole("button", {
      name: new RegExp(`Open review for ${escapeRegExp(media.title)}`),
    })
    .click();
  await expect(page.getByRole("heading", { name: media.title })).toBeVisible();
  const review = await reviewForTitle(api, media.title);
  expect(review).toBeTruthy();
  expect(
    review!.fields
      .slice()
      .sort(
        (left, right) =>
          left.templateFieldPosition - right.templateFieldPosition,
      )
      .map((field) => ({
        templateFieldId: field.templateFieldId,
        templateFieldName: field.templateFieldName,
        templateFieldPosition: field.templateFieldPosition,
        value: field.value,
      })),
  ).toEqual(
    fields.map((field, index) => ({
      templateFieldId: field.id,
      templateFieldName: field.name,
      templateFieldPosition: field.position,
      value: index + 3,
    })),
  );
  const displayedFieldPositions = await Promise.all(
    reorderedNames.map((fieldName) =>
      page
        .getByText(fieldName, { exact: true })
        .last()
        .evaluate((element) => element.getBoundingClientRect().top),
    ),
  );
  expect(displayedFieldPositions).toEqual(
    [...displayedFieldPositions].sort((left, right) => left - right),
  );
});
