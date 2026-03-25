const fs = require("fs");
const path = require("path");
const { spawn } = require("child_process");

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

function toElapsedMs(start) {
  return Date.now() - start;
}

async function taskKill(pid) {
  if (!pid) {
    return;
  }

  await new Promise(resolve => {
    const killer = spawn("taskkill", ["/pid", String(pid), "/t", "/f"], { stdio: "ignore" });
    killer.on("exit", () => resolve());
    killer.on("error", () => resolve());
  });
}

async function waitForText(page, expectedText, timeoutMs, pollMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() <= deadline) {
    const bodyText = await page.locator("body").innerText().catch(() => "");
    if (bodyText.includes(expectedText)) {
      return true;
    }

    await sleep(pollMs);
  }

  return false;
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
  const watchLogPath = outputPath.replace(/\.json$/i, ".watch.log");
  const filePath = path.resolve(args.file);
  const workingDir = path.resolve(args["working-dir"] || repoRoot);
  const url = args.url;
  const route = args.route || "/";
  const baselineText = args["baseline-text"];
  const expectedText = args["expected-text"];
  const searchText = args.search;
  const replaceText = args.replace;
  const timeoutMs = Number(args["timeout-ms"] || 180000);
  const pollMs = Number(args["poll-ms"] || 250);
  const readyLogPattern = new RegExp(args["ready-log-pattern"] || "Waiting for changes", "i");
  const watchArgsJson = args["watch-args-json"] || process.env.WATCH_ARGS_JSON || "";
  const envJson = args["env-json"] || process.env.WATCH_ENV_JSON || "";
  const watchArgs = watchArgsJson ? JSON.parse(watchArgsJson) : [];
  const extraEnv = envJson ? JSON.parse(envJson) : {};
  const variant = args.variant || "unnamed";

  fs.mkdirSync(path.dirname(outputPath), { recursive: true });

  const originalContent = fs.readFileSync(filePath, "utf8");
  if (!originalContent.includes(searchText)) {
    throw new Error(`Search text was not found in ${filePath}`);
  }

  const results = {
    variant,
    outputPath,
    watchLogPath,
    filePath,
    url,
    route,
    processStartUtc: isoNow(),
    watchArgs,
    extraEnv,
    initialVisibleMs: null,
    changeAppliedUtc: null,
    fileChangeLogMs: null,
    hotReloadSucceededMs: null,
    visibleWithoutRefreshMs: null,
    visibleAfterReloadMs: null,
    reloadIssuedAtMs: null,
    timedOut: false,
    watchLogExcerpt: []
  };

  let watchProcess = null;
  let browser = null;
  let page = null;
  let changeStartMs = null;
  let fileChangeSeen = false;
  let hotReloadSeen = false;
  let reloadIssued = false;
  const logEntries = [];

  try {
    if (watchArgs.length > 0) {
      watchProcess = spawn("dotnet", watchArgs, {
        cwd: workingDir,
        env: {
          ...process.env,
          ...extraEnv
        },
        windowsHide: true
      });

      const capture = streamName => data => {
        const text = data.toString();
        const lines = text.split(/\r?\n/).filter(Boolean);
        for (const line of lines) {
          const entry = {
            utc: isoNow(),
            elapsedMs: toElapsedMs(Date.parse(results.processStartUtc)),
            stream: streamName,
            line
          };
          logEntries.push(entry);

          if (changeStartMs !== null) {
            const changeElapsed = Date.now() - changeStartMs;
            if (!fileChangeSeen && /File (updated|changed|added|deleted):/i.test(line)) {
              fileChangeSeen = true;
              results.fileChangeLogMs = changeElapsed;
            }

            if (!hotReloadSeen && /Hot reload succeeded|Hot reload of static assets succeeded|No C# changes to apply/i.test(line)) {
              hotReloadSeen = true;
              results.hotReloadSucceededMs = changeElapsed;
            }
          }
        }
      };

      watchProcess.stdout.on("data", capture("stdout"));
      watchProcess.stderr.on("data", capture("stderr"));
    }

    browser = await chromium.launch({ headless: true });
    page = await browser.newPage({ viewport: { width: 1440, height: 900 } });

    const initialStart = Date.now();
    let baselineVisible = false;
    while (Date.now() - initialStart <= timeoutMs) {
      await page.goto(`${url.replace(/\/$/, "")}${route}`, { waitUntil: "domcontentloaded" }).catch(() => {});
      const bodyText = await page.locator("body").innerText().catch(() => "");
      if (bodyText.includes(baselineText)) {
        baselineVisible = true;
        results.initialVisibleMs = Date.now() - initialStart;
        break;
      }

      await sleep(1000);
    }

    if (!baselineVisible) {
      throw new Error(`Baseline text '${baselineText}' did not appear within ${timeoutMs} ms.`);
    }

    if (watchArgs.length > 0) {
      const readyDeadline = Date.now() + timeoutMs;
      while (Date.now() <= readyDeadline) {
        if (logEntries.some(entry => readyLogPattern.test(entry.line))) {
          break;
        }

        await sleep(250);
      }

      if (!logEntries.some(entry => readyLogPattern.test(entry.line))) {
        throw new Error(`Ready log pattern '${readyLogPattern}' did not appear within ${timeoutMs} ms.`);
      }
    }

    const changedContent = originalContent.replace(searchText, replaceText);
    changeStartMs = Date.now();
    results.changeAppliedUtc = isoNow();
    fs.writeFileSync(filePath, changedContent, "utf8");

    const changeDeadline = Date.now() + timeoutMs;
    while (Date.now() <= changeDeadline) {
      const bodyText = await page.locator("body").innerText().catch(() => "");
      if (bodyText.includes(expectedText)) {
        const elapsed = Date.now() - changeStartMs;
        if (reloadIssued) {
          results.visibleAfterReloadMs = elapsed;
        } else {
          results.visibleWithoutRefreshMs = elapsed;
          results.visibleAfterReloadMs = elapsed;
        }

        break;
      }

      if (!reloadIssued && hotReloadSeen) {
        reloadIssued = true;
        results.reloadIssuedAtMs = Date.now() - changeStartMs;
        await page.reload({ waitUntil: "domcontentloaded" }).catch(() => {});
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

    if (watchProcess) {
      await taskKill(watchProcess.pid);
    }
  }

  results.watchLogExcerpt = logEntries
    .filter(entry => /Now listening on|Hot reload|File (updated|changed|added|deleted):|Building|Waiting for changes|Evaluation completed|Projects loaded/i.test(entry.line))
    .slice(-40);

  fs.writeFileSync(
    watchLogPath,
    logEntries.map(entry => `[${entry.utc}] [${entry.stream}] ${entry.line}`).join("\n"),
    "utf8");
  fs.writeFileSync(outputPath, JSON.stringify(results, null, 2), "utf8");
  console.log(JSON.stringify(results, null, 2));
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
