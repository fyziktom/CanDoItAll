const fs = require("fs");
const path = require("path");

function parseArgs(argv) {
  const args = {};
  for (let index = 0; index < argv.length; index += 1) {
    const current = argv[index];
    if (!current.startsWith("--")) {
      continue;
    }

    const key = current.slice(2);
    const next = argv[index + 1];
    if (!next || next.startsWith("--")) {
      args[key] = "true";
      continue;
    }

    args[key] = next;
    index += 1;
  }

  return args;
}

function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

function isoNow() {
  return new Date().toISOString();
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
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

  const outputPath = path.resolve(args.output);
  const filePath = path.resolve(args.file);
  const url = args.url;
  const route = args.route || "/";
  const baselineText = args["baseline-text"];
  const expectedText = args["expected-text"];
  const searchText = args.search;
  const replaceText = args.replace;
  const timeoutMs = Number(args["timeout-ms"] || 60000);
  const pollMs = Number(args["poll-ms"] || 500);
  const variant = args.variant || "managed-watch";

  if (!url || !baselineText || !expectedText || !searchText || !replaceText) {
    throw new Error("Missing required arguments.");
  }

  fs.mkdirSync(path.dirname(outputPath), { recursive: true });

  const originalContent = fs.readFileSync(filePath, "utf8");
  if (!originalContent.includes(searchText)) {
    throw new Error(`Search text was not found in ${filePath}`);
  }

  const results = {
    variant,
    outputPath,
    filePath,
    url,
    route,
    processStartUtc: isoNow(),
    initialVisibleMs: null,
    changeAppliedUtc: null,
    visibleAfterReloadMs: null,
    reloadCount: 0,
    finalBodySnippet: null,
    timedOut: false
  };

  let browser = null;
  let page = null;

  try {
    browser = await chromium.launch({ headless: true });
    page = await browser.newPage({ viewport: { width: 1440, height: 900 } });

    const initialStart = Date.now();
    while (Date.now() - initialStart <= timeoutMs) {
      await page.goto(`${url.replace(/\/$/, "")}${route}`, { waitUntil: "domcontentloaded" }).catch(() => {});
      const bodyText = await page.locator("body").innerText().catch(() => "");
      if (bodyText.includes(baselineText)) {
        results.initialVisibleMs = Date.now() - initialStart;
        break;
      }

      await sleep(1000);
    }

    if (results.initialVisibleMs === null) {
      throw new Error(`Baseline text '${baselineText}' did not appear within ${timeoutMs} ms.`);
    }

    const changedContent = originalContent.replace(searchText, replaceText);
    const changeStartMs = Date.now();
    results.changeAppliedUtc = isoNow();
    fs.writeFileSync(filePath, changedContent, "utf8");

    const changeDeadline = Date.now() + timeoutMs;
    while (Date.now() <= changeDeadline) {
      await page.reload({ waitUntil: "domcontentloaded" }).catch(() => {});
      results.reloadCount += 1;

      const bodyText = await page.locator("body").innerText().catch(() => "");
      results.finalBodySnippet = bodyText.slice(0, 500);
      if (bodyText.includes(expectedText)) {
        results.visibleAfterReloadMs = Date.now() - changeStartMs;
        break;
      }

      await sleep(pollMs);
    }

    if (results.visibleAfterReloadMs === null) {
      results.timedOut = true;
    }
  } finally {
    try {
      fs.writeFileSync(filePath, originalContent, "utf8");
    } catch {
      // best effort
    }

    if (page) {
      try {
        await page.close();
      } catch {
        // best effort
      }
    }

    if (browser) {
      try {
        await browser.close();
      } catch {
        // best effort
      }
    }
  }

  fs.writeFileSync(outputPath, JSON.stringify(results, null, 2), "utf8");
  console.log(JSON.stringify(results, null, 2));
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
