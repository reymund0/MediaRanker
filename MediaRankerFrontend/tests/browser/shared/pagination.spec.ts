import { expect, test } from "@playwright/test";
import { createMockPageResult } from "./api-fixtures";

test("mock pagination slices a multi-page result and reports totals only when requested", () => {
  const items = Array.from({ length: 12 }, (_, index) => index);
  const secondPage = createMockPageResult(
    items,
    new URL("http://127.0.0.1/api/media?page=1&pageSize=10&includeTotalCount=true"),
  );
  expect(secondPage).toEqual({ items: [10, 11], totalCount: 12, page: 1, pageSize: 10 });

  const withoutCount = createMockPageResult(
    items,
    new URL("http://127.0.0.1/api/media?page=1&pageSize=10"),
  );
  expect(withoutCount).toEqual({ items: [10, 11], totalCount: null, page: 1, pageSize: 10 });
});
