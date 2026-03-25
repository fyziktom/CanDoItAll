const fs = require("fs");
const path = require("path");
const crypto = require("crypto");

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

function createRequestId(route) {
  return `${route}-${crypto.randomUUID()}`;
}

async function postTool(connection, route, payload) {
  const response = await fetch(`${connection.baseUrl}/api/tools/${route}`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-CanDoItAll-Backend-Token": connection.authToken,
      "X-CanDoItAll-RequestId": createRequestId(route)
    },
    body: JSON.stringify(payload)
  });

  const text = await response.text();
  if (!response.ok) {
    throw new Error(`Tool route '${route}' failed with HTTP ${response.status}: ${text}`);
  }

  const envelope = JSON.parse(text);
  if (!envelope.ok) {
    throw new Error(`Tool route '${route}' returned failure: ${text}`);
  }

  return envelope.data;
}

async function waitForStatusCondition(connection, sessionId, baselineCursor, timeoutMs, pollMs, predicate) {
  const startedAt = Date.now();
  let lastStatus = null;

  while (Date.now() - startedAt <= timeoutMs) {
    lastStatus = await postTool(connection, "app-status", { sessionId });
    if (predicate(lastStatus, baselineCursor)) {
      return {
        elapsedMs: Date.now() - startedAt,
        status: lastStatus
      };
    }

    await sleep(pollMs);
  }

  return {
    elapsedMs: null,
    status: lastStatus
  };
}

async function waitForTextWithReload(page, expectedText, timeoutMs, pollMs) {
  const startedAt = Date.now();
  let reloadCount = 0;
  let lastBodyText = "";

  while (Date.now() - startedAt <= timeoutMs) {
    await page.reload({ waitUntil: "domcontentloaded" }).catch(() => {});
    reloadCount += 1;

    lastBodyText = await page.locator("body").innerText().catch(() => "");
    if (lastBodyText.includes(expectedText)) {
      return {
        visibleAfterReloadMs: Date.now() - startedAt,
        reloadCount,
        lastBodySnippet: lastBodyText.slice(0, 500)
      };
    }

    await sleep(pollMs);
  }

  return {
    visibleAfterReloadMs: null,
    reloadCount,
    lastBodySnippet: lastBodyText.slice(0, 500)
  };
}

async function main() {
  const cliArgs = parseArgs(process.argv.slice(2));
  const args = cliArgs.config
    ? { ...JSON.parse(fs.readFileSync(path.resolve(cliArgs.config), "utf8")), ...cliArgs }
    : cliArgs;
  const repoRoot = path.resolve(__dirname, "..", "..");
  const registrationPath = path.resolve(args.registration || path.join(repoRoot, ".mcp-state", "backend", "registration.json"));
  const outputPath = path.resolve(args.output);
  const filePath = path.resolve(args.file);
  const route = args.route || "/projects";
  const baselineText = args["baseline-text"];
  const expectedText = args["expected-text"];
  const searchText = args.search;
  const replaceText = args.replace;
  const timeoutMs = Number(args["timeout-ms"] || 90000);
  const pollMs = Number(args["poll-ms"] || 500);
  const preEditDelayMs = Number(args["pre-edit-delay-ms"] || 0);
  const logicalAppId = args["logical-app-id"] || `bench-${crypto.randomUUID().replace(/-/g, "").slice(0, 10)}`;
  const variant = args.variant || "managed-mcp-watch";
  const waitFor = args["wait-for"] || "WatchReady";
  const reuseIfCompatible = String(args["reuse-if-compatible"] || "false").toLowerCase() === "true";
  const stopOnExit = String(args["stop-on-exit"] || "true").toLowerCase() !== "false";
  const environmentOverlay = args["environment-overlay"] || args.environmentOverlay || null;
  const playwrightPackage = process.env.PLAYWRIGHT_PACKAGE || path.join(
    repoRoot,
    "tests",
    "CanDoItAll.Tests.Playwright",
    "bin",
    "Debug",
    "net10.0",
    ".playwright",
    "package");

  if (!baselineText || !expectedText || !searchText || !replaceText) {
    throw new Error("Missing required benchmark text arguments.");
  }

  const registration = JSON.parse(fs.readFileSync(registrationPath, "utf8"));
  const connection = {
    baseUrl: registration.baseUrl,
    authToken: registration.authToken
  };

  const { chromium } = require(playwrightPackage);
  const originalContent = fs.readFileSync(filePath, "utf8");
  if (!originalContent.includes(searchText)) {
    throw new Error(`Search text was not found in ${filePath}`);
  }

  fs.mkdirSync(path.dirname(outputPath), { recursive: true });

  const results = {
    variant,
    logicalAppId,
    registrationPath,
    outputPath,
    route,
    processStartUtc: isoNow(),
    sessionId: null,
    url: null,
    baselineCursor: null,
    startupElapsedMs: null,
    initialVisibleMs: null,
    changeAppliedUtc: null,
    watchReportedAppliedElapsedMs: null,
    revisionConfirmedElapsedMs: null,
    revisionCondition: null,
    revisionWatch: null,
    revisionHealth: null,
    finalStatus: null,
    finalLogs: [],
    visibleAfterReloadMs: null,
    reloadCount: 0,
    lastBodySnippet: null,
    timedOut: false
  };

  let browser = null;
  let page = null;
  let sessionId = null;

  try {
    const startupStartedAt = Date.now();
    const appStart = await postTool(connection, "app-start", {
      logicalAppId,
      projectPath: path.join(repoRoot, "src", "CanDoItAll.Web", "CanDoItAll.Web.csproj"),
      mode: "WatchRun",
      launchType: "Project",
      preferredLane: "SourceWatch",
      configurationName: "Debug",
      reuseIfCompatible,
      conflictPolicy: "Replace",
      waitFor,
      environmentOverlay
    });

    sessionId = appStart.sessionId;
    results.sessionId = sessionId;
    results.startupElapsedMs = Date.now() - startupStartedAt;

    const appStatus = await postTool(connection, "app-status", { sessionId });
    const url = appStatus.observedUrls.find(candidate => candidate.startsWith("http://127.0.0.1:")) || appStatus.observedUrls[0];
    if (!url) {
      throw new Error("Managed app did not expose an observed URL.");
    }

    results.url = url;
    results.baselineCursor = appStatus.lastCursor;

    browser = await chromium.launch({ headless: true });
    page = await browser.newPage({ viewport: { width: 1440, height: 900 } });

    const baselineStartedAt = Date.now();
    while (Date.now() - baselineStartedAt <= timeoutMs) {
      await page.goto(`${url.replace(/\/$/, "")}${route}`, { waitUntil: "domcontentloaded" }).catch(() => {});
      const bodyText = await page.locator("body").innerText().catch(() => "");
      if (bodyText.includes(baselineText)) {
        results.initialVisibleMs = Date.now() - baselineStartedAt;
        break;
      }

      await sleep(1000);
    }

    if (results.initialVisibleMs === null) {
      throw new Error(`Baseline text '${baselineText}' did not appear within ${timeoutMs} ms.`);
    }

    if (preEditDelayMs > 0) {
      await sleep(preEditDelayMs);
    }

    const changedContent = originalContent.replace(searchText, replaceText);
    results.changeAppliedUtc = isoNow();
    fs.writeFileSync(filePath, changedContent, "utf8");

    const watchReportedAppliedPromise = waitForStatusCondition(
      connection,
      sessionId,
      results.baselineCursor,
      timeoutMs,
      500,
      status =>
        status.watch &&
        status.watch.lastHotReloadOutcome === "Succeeded" &&
        typeof status.watch.lastActivitySequence === "number" &&
        status.watch.lastActivitySequence > results.baselineCursor);

    const revisionConfirmedPromise = waitForStatusCondition(
      connection,
      sessionId,
      results.baselineCursor,
      timeoutMs,
      500,
      status =>
        status.watch &&
        status.watch.lastHotReloadOutcome === "Succeeded" &&
        typeof status.watch.lastActivitySequence === "number" &&
        status.watch.lastActivitySequence > results.baselineCursor &&
        status.revision &&
        status.revision.isConfirmed === true &&
        status.lastCursor > results.baselineCursor &&
        status.watch.pendingChange !== true);

    const watchReportedApplied = await watchReportedAppliedPromise;
    results.watchReportedAppliedElapsedMs = watchReportedApplied.elapsedMs;

    const revisionConfirmed = await revisionConfirmedPromise;
    results.revisionConfirmedElapsedMs = revisionConfirmed.elapsedMs;
    results.revisionCondition = "RevisionConfirmedByStatus";
    results.revisionWatch = revisionConfirmed.status ? revisionConfirmed.status.watch : null;
    results.revisionHealth = revisionConfirmed.status ? revisionConfirmed.status.health : null;

    const browserProbe = await waitForTextWithReload(page, expectedText, timeoutMs, pollMs);
    if (browserProbe.visibleAfterReloadMs !== null) {
      browserProbe.visibleAfterReloadMs += revisionConfirmed.elapsedMs ?? watchReportedApplied.elapsedMs ?? 0;
    } else {
      browserProbe.visibleAfterReloadMs = null;
    }
    results.visibleAfterReloadMs = browserProbe.visibleAfterReloadMs;
    results.reloadCount = browserProbe.reloadCount;
    results.lastBodySnippet = browserProbe.lastBodySnippet;
    results.timedOut = browserProbe.visibleAfterReloadMs === null;

    results.finalStatus = await postTool(connection, "app-status", { sessionId });
    const finalLogs = await postTool(connection, "app-logs", {
      sessionId,
      cursor: results.baselineCursor,
      limit: 200,
      includeStdOut: true,
      includeStdErr: true,
      includeSystemEvents: true,
      view: "Raw"
    });
    results.finalLogs = (finalLogs.entries || []).slice(-40).map(entry => ({
      sequence: entry.sequence,
      source: entry.source,
      text: entry.text
    }));
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

    if (sessionId && stopOnExit) {
      try {
        await postTool(connection, "app-stop", {
          sessionId,
          force: true,
          reason: "Managed MCP watch benchmark cleanup."
        });
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
