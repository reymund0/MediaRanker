import { expect, test } from "@playwright/test";
import { installApiMock, loginLocally } from "../shared/api-fixtures";

test("empty library and catalog offer their next actions without creating rows", async ({ page }) => {
  const api = await installApiMock(page);
  await loginLocally(page);

  await expect(page.getByText("No reviews yet")).toBeVisible();
  await expect(page.getByRole("main").getByRole("button", { name: "New review" })).toBeVisible();
  await page.goto("/media");
  await expect(page.getByRole("heading", { name: "Find something to rank" })).toBeVisible();
  await expect(page.getByText("No titles match “”")).toBeVisible();
  await expect(page.getByRole("button", { name: "Add a title" })).toHaveCount(2);
  expect([...api.reviews.values()].flat()).toHaveLength(0);
  await api.assertNoUnexpectedRequests();
});
