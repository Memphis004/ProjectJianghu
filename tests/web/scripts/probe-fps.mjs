// Per-tier frame rate of the HD-2D spike, measured in the built player.
//
// Stage 03 asks for the spike's FPS at each of the three quality tiers in a browser. That number
// only exists inside a running player (the tier is a URP asset, the camera is the game's own), so
// this script reads the player's own measurement (`__projectJianghuArenaSpike`) after pressing each
// tier button at the point the arena published it. It asserts nothing — a frame rate belongs in the
// report with its configuration, not in a gate that fails on a busy machine (ADR 0003) — and writes
// `artifacts/web-fps.json` plus a table on stdout.
//
//   node tests/web/scripts/probe-fps.mjs [baseUrl]
//
// The player must be served (`tools/serve-web.mjs`) from a Release build: a Development build's
// numbers may not be quoted (ADR 0003).

import { mkdir, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { chromium } from "@playwright/test";

import { ARENA_SPIKE } from "../globals.mjs";

const baseUrl = process.argv[2] ?? process.env.BASE_URL ?? "http://127.0.0.1:8080";
const outputPath = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "../../../artifacts/web-fps.json",
);

/** Frames one tier must accumulate before its window is quoted. */
const MIN_FRAMES = 90;

const browser = await chromium.launch({
  channel: "chrome",
  args: ["--use-angle=default", "--enable-unsafe-swiftshader"],
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
const errors = [];
page.on("console", (message) => {
  if (message.type() === "error") {
    errors.push(message.text());
  }
});
page.on("pageerror", (error) => errors.push(error.message));

const readSpike = () => page.evaluate((name) => window[name] ?? null, ARENA_SPIKE);

console.log(`opening ${baseUrl}/?scene=Arena`);
await page.goto(`${baseUrl}/?scene=Arena`, { waitUntil: "domcontentloaded" });
await page.waitForFunction((name) => Boolean(window[name]), ARENA_SPIKE, {
  timeout: 180_000,
  polling: 500,
});

const waitForSample = async (tier, minimumFrames) => {
  await page.waitForFunction(
    ({ name, expectedTier, frames }) => {
      const report = window[name];
      return (
        Boolean(report) &&
        report.quality?.tier === expectedTier &&
        report.sample?.frames >= frames
      );
    },
    { name: ARENA_SPIKE, expectedTier: tier, frames: minimumFrames },
    { timeout: 120_000, polling: 500 },
  );
};

const clickTier = async (report, name) => {
  const button = report.buttons?.find((candidate) => candidate.name === name);
  if (!button) {
    throw new Error(`the arena published no '${name}' tier button: ${JSON.stringify(report.buttons)}`);
  }

  const box = await page.locator("canvas").boundingBox();
  if (!box) {
    throw new Error("the player has no canvas element");
  }

  await page.mouse.click(
    box.x + (button.x / report.sample.canvasWidth) * box.width,
    box.y + ((report.sample.canvasHeight - button.y) / report.sample.canvasHeight) * box.height,
  );
};

const measured = [];
for (const tier of ["Low", "Medium", "High"]) {
  const before = await readSpike();
  await clickTier(before, tier.toLowerCase());
  await waitForSample(tier, MIN_FRAMES);

  const report = await readSpike();
  measured.push({
    tier,
    level: report.quality.level,
    renderScale: report.quality.renderScale,
    msaa: report.quality.msaa,
    frames: report.sample.frames,
    seconds: report.sample.seconds,
    fps: report.sample.fps,
    onePercentLowFps: report.sample.onePercentLowFps,
    worstMillis: report.sample.worstMillis,
    canvasWidth: report.sample.canvasWidth,
    canvasHeight: report.sample.canvasHeight,
    sorting: report.sorting,
  });
}

const summary = {
  measuredAt: new Date().toISOString(),
  url: `${baseUrl}/?scene=Arena`,
  browser: await browser.version(),
  viewport: { width: 1280, height: 720 },
  tiers: measured,
  pageErrors: errors,
};

await mkdir(dirname(outputPath), { recursive: true });
await writeFile(outputPath, `${JSON.stringify(summary, null, 2)}\n`, "utf8");

console.table(
  measured.map((row) => ({
    tier: row.tier,
    renderScale: row.renderScale,
    msaa: row.msaa,
    fps: row.fps.toFixed(1),
    onePercentLow: row.onePercentLowFps.toFixed(1),
    worstMillis: row.worstMillis.toFixed(1),
    canvas: `${row.canvasWidth}x${row.canvasHeight}`,
    sortingPassed: row.sorting?.passed,
  })),
);
console.log(`written to ${outputPath}`);

await browser.close();
