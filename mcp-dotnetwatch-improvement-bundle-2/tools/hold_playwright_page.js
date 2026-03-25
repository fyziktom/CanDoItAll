const path = require("path");

async function main() {
  const url = process.argv[2];
  const waitMs = Number(process.argv[3] || "60000");
  if (!url) {
    throw new Error("Usage: node hold_playwright_page.js <url> [waitMs]");
  }

  const repoRoot = path.resolve(__dirname, "..", "..");
  const playwrightPackage = process.env.PLAYWRIGHT_PACKAGE || path.join(
    repoRoot,
    "tests",
    "CanDoItAll.Tests.Playwright",
    "bin",
    "Debug",
    "net10.0",
    ".playwright",
    "package");

  const { chromium } = require(playwrightPackage);
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });

  try {
    await page.goto(url, { waitUntil: "networkidle" });
    await page.waitForTimeout(waitMs);
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
