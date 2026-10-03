import { expect } from "../shared/fixtures";
import type { E2EApi, ReviewDto, TemplateDto } from "../shared/fixtures";

export async function reviewForTitle(api: E2EApi, title: string) {
  const reviews = await api.get<ReviewDto[]>(
    "/api/reviews/byMediaType/VideoGame",
  );
  return reviews.find((review) => review.mediaTitle === title);
}

export async function scoreAllFields(
  page: import("@playwright/test").Page,
  fields: TemplateDto["fields"],
  values: number[],
) {
  for (const [index, field] of [...fields]
    .sort((a, b) => a.position - b.position)
    .entries()) {
    await page
      .getByRole("group", { name: field.name })
      .getByRole("button", { name: `${values[index]} out of 10` })
      .click();
  }
}

export async function startNewReviewFromCatalog(
  page: import("@playwright/test").Page,
  title: string,
) {
  await page.goto("/media?mediaType=VideoGame");
  const search = page.getByRole("textbox", { name: "Search the catalog" });
  await search.fill(title);
  await expect(page.getByText(title, { exact: true })).toBeVisible();
  const reviewButton = page.getByRole("button", {
    name: "Review",
    exact: true,
  });
  await expect(reviewButton).toHaveCount(1);
  await reviewButton.click();
  await expect(page.getByRole("dialog", { name: "New review" })).toBeVisible();
}
