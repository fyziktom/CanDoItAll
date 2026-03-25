const fs = require("fs");
const path = require("path");

const repoRoot = path.resolve(__dirname, "..", "..");
const defaultPlaywrightPackage = path.join(
  repoRoot,
  "tests",
  "CanDoItAll.Tests.Playwright",
  "bin",
  "Debug",
  "net10.0",
  ".playwright",
  "package");

const { chromium } = require(process.env.PLAYWRIGHT_PACKAGE || defaultPlaywrightPackage);

const baseUrl = (process.env.CANDOITALL_PLAYWRIGHT_BASEURL || process.argv[2] || "http://127.0.0.1:5502").replace(/\/$/, "");
const outputDir = path.resolve(
  process.argv[3] || path.join(repoRoot, "mcp-improvements-bundle-1-using-experiences", "artifacts", "projects-page-scan"));

fs.mkdirSync(outputDir, { recursive: true });

const samples = [
  { name: "Viewport Alpha", phase: "Discovery" },
  { name: "Viewport Beta", phase: "Planning" },
  { name: "Viewport Gamma", phase: "Build" },
  { name: "Viewport Delta", phase: "Review" }
];

const viewports = [
  { key: "desktop", width: 1440, height: 900 },
  { key: "laptop", width: 1280, height: 800 },
  { key: "mobile", width: 390, height: 844 }
];

async function createProject(page, projectName, phase) {
  await page.goto(`${baseUrl}/projects`);
  await page.getByTestId("projects-new-button").waitFor();
  await page.waitForTimeout(1500);
  await page.getByTestId("projects-new-button").click({ force: true });
  await page.getByTestId("project-name-input").waitFor();
  await page.getByTestId("project-name-input").fill(projectName);
  await page.locator('input[name="editor.CurrentPhase"]').fill(phase);
  await page.getByTestId("project-save-button").click();
  await page.getByText("Project saved.").waitFor();
  await page.getByRole("button", { name: "Close", exact: true }).click();
  await page.locator("[data-testid='projects-editor-modal']").waitFor({ state: "hidden" });
}

async function ensureProjects(page) {
  await page.goto(`${baseUrl}/projects`);
  await page.getByTestId("projects-new-button").waitFor();

  const existingCards = await page.locator("[data-testid='project-card']").count();
  if (existingCards >= samples.length) {
    return;
  }

  const stamp = new Date().toISOString().replace(/[:.]/g, "-");
  for (let index = existingCards; index < samples.length; index += 1) {
    const sample = samples[index];
    await createProject(page, `${sample.name} ${stamp}`, sample.phase);
  }
}

async function captureViewport(page, viewport) {
  await page.setViewportSize({ width: viewport.width, height: viewport.height });
  await page.goto(`${baseUrl}/projects`);
  await page.getByTestId("projects-new-button").waitFor();
  await page.waitForTimeout(500);

  const metrics = await page.evaluate(() => {
    const doc = document.scrollingElement || document.documentElement;
    const scrollHost = document.querySelector("[data-testid='projects-cards-scroll']");
    const commandBar = document.querySelector("[data-testid='projects-command-bar']");
    const cards = Array.from(document.querySelectorAll("[data-testid='project-card']")).map((card, index) => {
      const rect = card.getBoundingClientRect();
      return {
        index,
        top: Math.round(rect.top),
        bottom: Math.round(rect.bottom),
        height: Math.round(rect.height)
      };
    });

    const search = document.querySelector('input[placeholder="Search by name or current phase"]');
    const searchRect = search ? search.getBoundingClientRect() : null;

    return {
      pageTitle: document.title,
      viewport: {
        width: window.innerWidth,
        height: window.innerHeight
      },
      documentHeight: Math.round(doc.scrollHeight),
      documentClientHeight: Math.round(doc.clientHeight),
      pageNeedsVerticalScroll: doc.scrollHeight > doc.clientHeight + 2,
      commandBarBottom: commandBar ? Math.round(commandBar.getBoundingClientRect().bottom) : null,
      projectCardCount: cards.length,
      firstCard: cards[0] ?? null,
      scrollHostHeight: scrollHost ? Math.round(scrollHost.clientHeight) : null,
      scrollHostScrollHeight: scrollHost ? Math.round(scrollHost.scrollHeight) : null,
      scrollHostNeedsVerticalScroll: scrollHost ? scrollHost.scrollHeight > scrollHost.clientHeight + 2 : null,
      searchTop: searchRect ? Math.round(searchRect.top) : null,
      searchBottom: searchRect ? Math.round(searchRect.bottom) : null
    };
  });

  const screenshotPath = path.join(outputDir, `${viewport.key}.png`);
  await page.screenshot({ path: screenshotPath, fullPage: false });
  return {
    viewport,
    screenshotPath,
    metrics
  };
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext();
  const page = await context.newPage();
  const skipSeed = /^1|true$/i.test(process.env.SKIP_SEED || "");

  try {
    if (!skipSeed) {
      await ensureProjects(page);
    }

    const results = [];
    for (const viewport of viewports) {
      results.push(await captureViewport(page, viewport));
    }

    const report = {
      capturedAtUtc: new Date().toISOString(),
      baseUrl,
      outputDir,
      results
    };

    const reportPath = path.join(outputDir, "metrics.json");
    fs.writeFileSync(reportPath, JSON.stringify(report, null, 2));
    console.log(JSON.stringify({ ok: true, reportPath, outputDir }, null, 2));
  } finally {
    await page.close();
    await context.close();
    await browser.close();
  }
}

main().catch(error => {
  console.error(error && error.stack ? error.stack : error);
  process.exitCode = 1;
});
