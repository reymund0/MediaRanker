import { expect } from "../shared/fixtures";
import type { E2EApi, TemplateDto } from "../shared/fixtures";

export async function gameTemplate(api: E2EApi): Promise<TemplateDto> {
  const templates = await api.get<TemplateDto[]>("/api/templates/VideoGame");
  const template = templates.find((candidate) => candidate.isSystem);
  expect(
    template,
    "the migration-seeded Video Game template is required",
  ).toBeTruthy();
  return template!;
}
