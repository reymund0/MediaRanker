import { test, expect, signInAsLocalTestUser } from "../shared/fixtures";
import type { MediaDto } from "../shared/fixtures";

test("catalog search keeps the query and resets pagination on category change", async ({
  e2eApi: api,
  page,
}) => {
  const prefix = "E2E search collection ";
  const games: MediaDto[] = [];
  for (let index = 1; index <= 12; index++) {
    games.push(
      await api.createMedia(`${prefix}game ${String(index).padStart(2, "0")}`),
    );
  }
  const movie = await api.createMedia(`${prefix}movie`, "Movie");

  await signInAsLocalTestUser(page);
  await page.goto("/media?mediaType=VideoGame");
  const search = page.getByRole("textbox", { name: "Search the catalog" });
  await search.fill(prefix);
  await expect(page.getByText(`Showing 1–10 of 12`)).toBeVisible();
  await expect(
    page.getByText(`${prefix}game 01`, { exact: true }),
  ).toBeVisible();
  await expect(page.getByText(`${prefix}game 12`, { exact: true })).toHaveCount(
    0,
  );
  await page.getByRole("button", { name: "Next page" }).click();
  await expect(page.getByText(`Showing 11–12 of 12`)).toBeVisible();
  await expect(
    page.getByText(`${prefix}game 12`, { exact: true }),
  ).toBeVisible();

  await page.getByRole("button", { name: "Movies", exact: true }).click();
  await expect(search).toHaveValue(prefix);
  await expect(page.getByText("Showing 1–1 of 1")).toBeVisible();
  await expect(page.getByText(movie.title, { exact: true })).toBeVisible();

  await search.fill("E2E search no such title");
  await expect(
    page.getByRole("heading", { name: /No titles match/ }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Add a title" }).last(),
  ).toBeVisible();
});
