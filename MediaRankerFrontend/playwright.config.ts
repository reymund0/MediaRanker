import { defineConfig, devices } from "@playwright/test";
import path from "node:path";

const baseURL = process.env.E2E_BASE_URL ?? "http://127.0.0.1:3100";
const artifactDir = path.resolve(process.env.E2E_RUN_DIR ?? process.cwd());

export default defineConfig({
  testDir: "./tests",
  fullyParallel: false,
  forbidOnly: Boolean(process.env.CI),
  retries: 0,
  workers: 1,
  reporter: [
    ["list"],
    [
      "html",
      {
        outputFolder: path.join(artifactDir, "playwright-report"),
        open: "never",
      },
    ],
  ],
  outputDir: path.join(artifactDir, "test-results"),
  use: {
    baseURL,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    ...devices["Desktop Chrome"],
  },
  projects: [
    {
      name: "e2e",
      testDir: "./tests/e2e",
      use: { ...devices["Desktop Chrome"] },
    },
    {
      name: "browser",
      testDir: "./tests/browser",
      use: { ...devices["Desktop Chrome"] },
    },
  ],
});
