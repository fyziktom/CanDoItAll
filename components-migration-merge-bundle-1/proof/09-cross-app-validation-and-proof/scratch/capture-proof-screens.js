const path = require("node:path");
const fs = require("node:fs/promises");
const { chromium } = require("C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Playwright/bin/Debug/net10.0/.playwright/package");

const proofRoot = "C:/repositories/CanDoItAll/components-migration-merge-bundle-1/proof/09-cross-app-validation-and-proof";
const sandboxDir = path.join(proofRoot, "screenshots", "sandbox");
const candoDir = path.join(proofRoot, "screenshots", "candoitall");
const zyDir = path.join(proofRoot, "screenshots", "zyphonote");
const sandboxBase = "http://127.0.0.1:61261";
const candoBase = "http://127.0.0.1:61262";
const zyBase = "http://127.0.0.1:61263";
const learningPackageId = "a18f30b8-c62b-4691-8de8-35f7d32ad4ac";

async function ensureDirs() {
  await fs.mkdir(sandboxDir, { recursive: true });
  await fs.mkdir(candoDir, { recursive: true });
  await fs.mkdir(zyDir, { recursive: true });
}

async function waitForStablePage(page, extraWait = 1000) {
  await page.waitForLoadState("domcontentloaded");
  try {
    await page.waitForLoadState("networkidle", { timeout: 10000 });
  } catch {}
  await page.waitForTimeout(extraWait);
}

function attachPageDiagnostics(page, label) {
  page.on("pageerror", error => {
    console.error(`Page error (${label}): ${error.message}`);
  });
  page.on("console", message => {
    if (message.type() === "error") {
      console.error(`Console error (${label}): ${message.text()}`);
    }
  });
}

async function waitForCanvasInk(page, selector, label) {
  await page.locator(selector).waitFor();
  await page.waitForFunction(currentSelector => {
    const canvas = document.querySelector(currentSelector);
    if (!(canvas instanceof HTMLCanvasElement) || canvas.width === 0 || canvas.height === 0) {
      return false;
    }

    const context = canvas.getContext("2d", { willReadFrequently: true });
    if (!context) {
      return false;
    }

    try {
      const pixels = context.getImageData(0, 0, canvas.width, canvas.height).data;
      for (let index = 0; index < pixels.length; index += 160) {
        const alpha = pixels[index + 3];
        if (alpha === 0) {
          continue;
        }

        const red = pixels[index];
        const green = pixels[index + 1];
        const blue = pixels[index + 2];
        if (red < 250 || green < 250 || blue < 250) {
          return true;
        }
      }
    } catch {
      return false;
    }

    return false;
  }, selector, { timeout: 15000 });
  console.log(`Canvas ink ready: ${label}`);
}

async function logCalendarMetrics(page, label) {
  const metrics = await page.evaluate(() => {
    const host = document.querySelector(".cdi-canvas-calendar-host");
    const controller = host && host.__debugCalendarController ? host.__debugCalendarController : null;
    const canvasElement = document.querySelector(".zy-calendar-canvas");
    const describe = selector => {
      const element = document.querySelector(selector);
      if (!(element instanceof HTMLElement)) {
        return null;
      }

      const rect = element.getBoundingClientRect();
      const style = window.getComputedStyle(element);
      const result = {
        width: Math.round(rect.width),
        height: Math.round(rect.height),
        display: style.display,
        overflow: style.overflow,
        className: element.className
      };

      if (element instanceof HTMLCanvasElement) {
        result.canvasWidth = element.width;
        result.canvasHeight = element.height;
      }

      return result;
    };

    const readPixel = (x, y) => {
      if (!(canvasElement instanceof HTMLCanvasElement)) {
        return null;
      }

      const context = canvasElement.getContext("2d", { willReadFrequently: true });
      if (!context) {
        return null;
      }

      const sampleX = Math.max(0, Math.min(canvasElement.width - 1, Math.round(x)));
      const sampleY = Math.max(0, Math.min(canvasElement.height - 1, Math.round(y)));
      const pixels = context.getImageData(sampleX, sampleY, 1, 1).data;
      return {
        x: sampleX,
        y: sampleY,
        rgba: [pixels[0], pixels[1], pixels[2], pixels[3]]
      };
    };

    const buildPixelSamples = timedLayout => {
      if (!timedLayout) {
        return null;
      }

      const firstDayRect = timedLayout.dayRects?.[0] ?? null;
      const firstTimedItem = timedLayout.timedItems?.[0] ?? null;
      const secondTimedItem = timedLayout.timedItems?.[1] ?? null;
      return {
        sidebarTop: readPixel(24, 60),
        mainHeader: readPixel(timedLayout.mainX + 24, timedLayout.stageY + 20),
        firstDayTop: firstDayRect ? readPixel(firstDayRect.x + 12, timedLayout.bodyY + 16) : null,
        firstDayMiddle: firstDayRect ? readPixel(firstDayRect.x + (firstDayRect.width / 2), timedLayout.bodyY + 140) : null,
        firstEventCenter: firstTimedItem?.bounds
          ? readPixel(firstTimedItem.bounds.x + (firstTimedItem.bounds.width / 2), firstTimedItem.bounds.y + (firstTimedItem.bounds.height / 2))
          : null,
        secondEventCenter: secondTimedItem?.bounds
          ? readPixel(secondTimedItem.bounds.x + (secondTimedItem.bounds.width / 2), secondTimedItem.bounds.y + (secondTimedItem.bounds.height / 2))
          : null
      };
    };

    const timedLayout = controller?.state?.layoutCache?.timed
      ? {
          mainX: controller.state.layoutCache.timed.mainX,
          stageY: controller.state.layoutCache.timed.stageY,
          bodyY: controller.state.layoutCache.timed.bodyY,
          minuteHeight: controller.state.layoutCache.timed.minuteHeight,
          dayWidth: controller.state.layoutCache.timed.dayWidth,
          dayRects: controller.state.layoutCache.timed.dayRects ?? [],
          firstDayRect: controller.state.layoutCache.timed.dayRects?.[0] ?? null,
          timedItems: Array.isArray(controller.state.layoutCache.timed.timedItems)
            ? controller.state.layoutCache.timed.timedItems.slice(0, 3).map(item => ({
                title: item?.event?.title ?? null,
                startMinutes: item?.startMinutes ?? null,
                endMinutes: item?.endMinutes ?? null,
                columns: item?.columns ?? null,
                column: item?.column ?? null,
                bounds: item?.bounds ?? null
              }))
            : []
        }
      : null;

    return {
      devicePixelRatio: window.devicePixelRatio,
      body: describe(".zy-calendar-body"),
      stage: describe(".zy-calendar-stage"),
      stageShell: describe(".zy-calendar-stage-shell"),
      canvasShell: describe(".zy-calendar-canvas-shell"),
      canvas: describe(".zy-calendar-canvas"),
      loading: describe(".zy-calendar-loading"),
      pixels: buildPixelSamples(timedLayout),
      controller: controller ? {
        view: controller.state?.view,
        selectedDateKey: controller.state?.selectedDateKey,
        businessHoursStart: controller.options?.businessHoursStart,
        businessHoursEnd: controller.options?.businessHoursEnd,
        visibleEvents: Array.isArray(controller.state?.visibleEvents) ? controller.state.visibleEvents.length : null,
        timedLayout
      } : null
    };
  });
  console.log(`Calendar metrics (${label}): ${JSON.stringify(metrics)}`);
}

async function logSellerProfileMetrics(page, label) {
  const metrics = await page.evaluate(() => {
    const measure = element => {
      if (!(element instanceof HTMLElement)) {
        return null;
      }

      const rect = element.getBoundingClientRect();
      return {
        width: Math.round(rect.width),
        height: Math.round(rect.height),
        className: element.className
      };
    };

    const displayNameInput = document.querySelector("#seller_display_name");
    return {
      coreLayout: measure(document.querySelector(".zy-seller-profile-core-layout")),
      coreStack: measure(document.querySelector(".zy-seller-profile-core-stack")),
      identityCard: measure(document.querySelector(".zy-seller-profile-core-stack .zy-seller-profile-subcard")),
      displayNameField: measure(displayNameInput?.closest(".zy-seller-profile-field")),
      displayNameInput: measure(displayNameInput),
      displayNameValue: displayNameInput instanceof HTMLInputElement ? displayNameInput.value : null
    };
  });
  console.log(`Seller profile metrics (${label}): ${JSON.stringify(metrics)}`);
}

async function withCanvasCaptureSnapshot(page, action) {
  await page.evaluate(() => {
    document.querySelectorAll('[data-proof-canvas-capture="true"]').forEach(node => node.remove());
    document.querySelectorAll("canvas").forEach(canvas => {
      if (!(canvas instanceof HTMLCanvasElement) || !canvas.parentElement) {
        return;
      }

      const image = document.createElement("img");
      image.src = canvas.toDataURL("image/png");
      image.alt = "";
      image.setAttribute("data-proof-canvas-capture", "true");
      image.style.position = "absolute";
      image.style.inset = "0";
      image.style.width = "100%";
      image.style.height = "100%";
      image.style.display = "block";
      image.style.pointerEvents = "none";
      image.style.zIndex = "1";
      canvas.dataset.proofOriginalVisibility = canvas.style.visibility;
      canvas.style.visibility = "hidden";
      canvas.parentElement.appendChild(image);
    });
  });

  try {
    await action();
  } finally {
    await page.evaluate(() => {
      document.querySelectorAll('[data-proof-canvas-capture="true"]').forEach(node => node.remove());
      document.querySelectorAll("canvas").forEach(canvas => {
        if (!(canvas instanceof HTMLCanvasElement)) {
          return;
        }

        const originalVisibility = canvas.dataset.proofOriginalVisibility ?? "";
        canvas.style.visibility = originalVisibility;
        delete canvas.dataset.proofOriginalVisibility;
      });
    });
  }
}

async function capture(page, filePath) {
  await withCanvasCaptureSnapshot(page, async () => {
    await page.screenshot({ path: filePath, fullPage: true });
  });
}

async function captureElement(page, selector, filePath) {
  await withCanvasCaptureSnapshot(page, async () => {
    await page.locator(selector).first().screenshot({ path: filePath });
  });
}

async function createProject(page, name, phase) {
  await page.goto(`${candoBase}/projects`, { waitUntil: "domcontentloaded" });
  await page.getByTestId("projects-new-button").waitFor();
  await page.getByTestId("projects-new-button").click();
  try {
    await page.getByTestId("project-name-input").waitFor({ timeout: 2000 });
  } catch {
    await page.getByTestId("projects-new-button").click();
    await page.getByTestId("project-name-input").waitFor();
  }

  await page.getByTestId("project-name-input").fill(name);
  await page.locator('input[name="editor.CurrentPhase"]').fill(phase);
  await Promise.all([
    page.waitForURL(/\/projects\/.+\/structure$/i),
    page.getByRole("button", { name: "Save and open structure", exact: true }).click()
  ]);

  const match = page.url().match(/\/projects\/(?<projectId>[0-9a-fA-F-]+)\/structure$/i);
  if (!match || !match.groups || !match.groups.projectId) {
    throw new Error(`Could not parse project id from ${page.url()}`);
  }

  return match.groups.projectId;
}

async function login(page, email, password, returnPath) {
  const encodedReturnPath = encodeURIComponent(returnPath);
  await page.goto(`${zyBase}/account/login?returnUrl=${encodedReturnPath}`, { waitUntil: "domcontentloaded" });
  await waitForStablePage(page, 800);
  await page.fill("[data-testid='login-email']", email);
  await page.fill("[data-testid='login-password']", password);
  await page.click("[data-testid='login-submit']");
  await page.waitForFunction(
    expectedPath => `${window.location.pathname}${window.location.search}` === expectedPath && !document.querySelector("[data-testid='login-page-root']"),
    returnPath,
    { timeout: 30000 });
  await waitForStablePage(page, 1200);
}

async function captureSandbox(browser) {
  const context = await browser.newContext({ viewport: { width: 1600, height: 1200 } });
  const page = await context.newPage();
  attachPageDiagnostics(page, "sandbox");
  const groups = [
    "foundations",
    "inputs",
    "actions",
    "navigation",
    "feedback",
    "layout",
    "data-display",
    "overlays",
    "canvas"
  ];
  const scenarios = [
    ["dense-content", "dense"],
    ["empty-state", "empty"]
  ];
  const frames = [
    ["desktop", "desktop"],
    ["mobile", "mobile"]
  ];

  for (const group of groups) {
    for (const [scenario, scenarioLabel] of scenarios) {
      for (const [frame, frameLabel] of frames) {
        const url = `${sandboxBase}/groups/${group}?scenario=${scenario}&frame=${frame}`;
        await page.goto(url, { waitUntil: "domcontentloaded" });
        await page.locator("[data-testid='sandbox-demo-surface']").waitFor();
        await waitForStablePage(page, 600);
        await capture(page, path.join(sandboxDir, `sandbox-${group}-${scenarioLabel}-${frameLabel}.png`));
      }
    }
  }

  await context.close();
}

async function captureCanDoItAll(browser) {
  const desktopContext = await browser.newContext({ viewport: { width: 1600, height: 1200 } });
  const desktopPage = await desktopContext.newPage();
  attachPageDiagnostics(desktopPage, "candoitall-desktop");
  const projectId = await createProject(desktopPage, `Bundle Proof ${Date.now()}`, "Discovery");

  await desktopPage.goto(`${candoBase}/projects`, { waitUntil: "domcontentloaded" });
  await desktopPage.getByTestId("projects-new-button").waitFor();
  await waitForStablePage(desktopPage, 700);
  await capture(desktopPage, path.join(candoDir, "candoitall-projects-desktop.png"));

  await desktopPage.goto(`${candoBase}/validation`, { waitUntil: "domcontentloaded" });
  await desktopPage.locator("main").first().waitFor();
  await waitForStablePage(desktopPage, 1200);
  await capture(desktopPage, path.join(candoDir, "candoitall-validation-desktop.png"));

  await desktopPage.goto(`${candoBase}/test-lab`, { waitUntil: "domcontentloaded" });
  await desktopPage.getByText("Tests, evidence, and execution results").waitFor();
  await waitForStablePage(desktopPage, 700);
  await capture(desktopPage, path.join(candoDir, "candoitall-test-lab-desktop.png"));

  await desktopPage.goto(`${candoBase}/prompt-factory`, { waitUntil: "domcontentloaded" });
  await desktopPage.getByText("Prompt session workbench").waitFor();
  await waitForStablePage(desktopPage, 1200);
  await capture(desktopPage, path.join(candoDir, "candoitall-prompt-factory-desktop.png"));

  await desktopPage.goto(`${candoBase}/projects/${projectId}/structure`, { waitUntil: "domcontentloaded" });
  await desktopPage.locator(".cw-workbench-shell").waitFor();
  await waitForStablePage(desktopPage, 1200);
  await capture(desktopPage, path.join(candoDir, "candoitall-structure-desktop.png"));

  await desktopPage.goto(`${candoBase}/projects/${projectId}/calendar`, { waitUntil: "domcontentloaded" });
  await desktopPage.getByRole("heading", { name: "Project calendar", exact: true }).waitFor();
  await waitForStablePage(desktopPage, 1200);
  await logCalendarMetrics(desktopPage, "candoitall-calendar-desktop");
  await capture(desktopPage, path.join(candoDir, "candoitall-calendar-desktop.png"));
  await captureElement(desktopPage, ".zy-calendar-body", path.join(candoDir, "candoitall-calendar-focus-desktop.png"));
  await desktopContext.close();

  const mobileContext = await browser.newContext({ viewport: { width: 390, height: 844 } });
  const mobilePage = await mobileContext.newPage();
  attachPageDiagnostics(mobilePage, "candoitall-mobile");

  await mobilePage.goto(`${candoBase}/projects`, { waitUntil: "domcontentloaded" });
  await mobilePage.getByTestId("projects-new-button").waitFor();
  await waitForStablePage(mobilePage, 700);
  await capture(mobilePage, path.join(candoDir, "candoitall-projects-mobile.png"));

  await mobilePage.goto(`${candoBase}/projects/${projectId}/structure`, { waitUntil: "domcontentloaded" });
  await mobilePage.locator(".cw-workbench-shell").waitFor();
  await waitForStablePage(mobilePage, 1200);
  await capture(mobilePage, path.join(candoDir, "candoitall-structure-mobile.png"));

  await mobilePage.goto(`${candoBase}/projects/${projectId}/calendar`, { waitUntil: "domcontentloaded" });
  await mobilePage.getByRole("heading", { name: "Project calendar", exact: true }).waitFor();
  await waitForStablePage(mobilePage, 1200);
  await logCalendarMetrics(mobilePage, "candoitall-calendar-mobile");
  await capture(mobilePage, path.join(candoDir, "candoitall-calendar-mobile.png"));
  await captureElement(mobilePage, ".zy-calendar-body", path.join(candoDir, "candoitall-calendar-focus-mobile.png"));

  await mobileContext.close();
}

async function captureZyphonote(browser) {
  const sellerDesktop = await browser.newContext({ viewport: { width: 1600, height: 1200 }, ignoreHTTPSErrors: true });
  const sellerPage = await sellerDesktop.newPage();
  attachPageDiagnostics(sellerPage, "zyphonote-seller-desktop");
  await login(sellerPage, "seller@zyphonote.local", "SellerPassword!12345", "/account/marketplace");
  await sellerPage.locator("[data-testid='account-marketplace-page-root']").waitFor();
  await waitForStablePage(sellerPage, 1200);
  await capture(sellerPage, path.join(zyDir, "zyphonote-marketplace-desktop.png"));

  await sellerPage.goto(`${zyBase}/account/playlists`, { waitUntil: "domcontentloaded" });
  await sellerPage.locator("[data-testid='account-playlists-page-root']").waitFor();
  await waitForStablePage(sellerPage, 1200);
  await capture(sellerPage, path.join(zyDir, "zyphonote-playlists-desktop.png"));

  await sellerPage.goto(`${zyBase}/account/events`, { waitUntil: "domcontentloaded" });
  await sellerPage.locator("[data-testid='account-events-page-root']").waitFor();
  await waitForStablePage(sellerPage, 1500);
  await waitForCanvasInk(sellerPage, ".zy-calendar-canvas", "zyphonote-events-desktop");
  await logCalendarMetrics(sellerPage, "zyphonote-events-desktop");
  await capture(sellerPage, path.join(zyDir, "zyphonote-events-desktop.png"));
  await captureElement(sellerPage, ".zy-calendar-body", path.join(zyDir, "zyphonote-events-focus-desktop.png"));

  await sellerPage.goto(`${zyBase}/account/learning/builder`, { waitUntil: "domcontentloaded" });
  await sellerPage.locator("[data-testid='account-learning-builder-page-root']").waitFor();
  await waitForStablePage(sellerPage, 1500);
  await capture(sellerPage, path.join(zyDir, "zyphonote-learning-builder-desktop.png"));

  await sellerPage.goto(`${zyBase}/account/my-scores`, { waitUntil: "domcontentloaded" });
  await sellerPage.locator("[data-testid='account-my-scores-page-root']").waitFor();
  await waitForStablePage(sellerPage, 1200);
  await capture(sellerPage, path.join(zyDir, "zyphonote-my-scores-desktop.png"));

  await sellerPage.goto(`${zyBase}/account/seller-profile`, { waitUntil: "domcontentloaded" });
  await sellerPage.locator("[data-testid='account-seller-profile-page-root']").waitFor();
  await waitForStablePage(sellerPage, 1500);
  await logSellerProfileMetrics(sellerPage, "desktop");
  await capture(sellerPage, path.join(zyDir, "zyphonote-seller-profile-desktop.png"));
  await sellerDesktop.close();

  const buyerDesktop = await browser.newContext({ viewport: { width: 1600, height: 1200 }, ignoreHTTPSErrors: true });
  const buyerDesktopPage = await buyerDesktop.newPage();
  attachPageDiagnostics(buyerDesktopPage, "zyphonote-buyer-desktop");
  const buyerReturnPath = `/account/learning/package?packageId=${learningPackageId}`;
  await login(buyerDesktopPage, "buyer@zyphonote.local", "BuyerPassword!12345", buyerReturnPath);
  await buyerDesktopPage.locator("[data-testid='account-learning-package-page-root']").waitFor();
  await waitForStablePage(buyerDesktopPage, 1500);
  await capture(buyerDesktopPage, path.join(zyDir, "zyphonote-learning-package-desktop.png"));
  await buyerDesktop.close();

  const sellerMobile = await browser.newContext({ viewport: { width: 390, height: 844 }, ignoreHTTPSErrors: true });
  const sellerMobilePage = await sellerMobile.newPage();
  attachPageDiagnostics(sellerMobilePage, "zyphonote-seller-mobile");
  await login(sellerMobilePage, "seller@zyphonote.local", "SellerPassword!12345", "/account/marketplace");
  await sellerMobilePage.locator("[data-testid='account-marketplace-page-root']").waitFor();
  await waitForStablePage(sellerMobilePage, 1200);
  await capture(sellerMobilePage, path.join(zyDir, "zyphonote-marketplace-mobile.png"));

  await sellerMobilePage.goto(`${zyBase}/account/playlists`, { waitUntil: "domcontentloaded" });
  await sellerMobilePage.locator("[data-testid='account-playlists-page-root']").waitFor();
  await waitForStablePage(sellerMobilePage, 1200);
  await capture(sellerMobilePage, path.join(zyDir, "zyphonote-playlists-mobile.png"));

  await sellerMobilePage.goto(`${zyBase}/account/seller-profile`, { waitUntil: "domcontentloaded" });
  await sellerMobilePage.locator("[data-testid='account-seller-profile-page-root']").waitFor();
  await waitForStablePage(sellerMobilePage, 1500);
  await logSellerProfileMetrics(sellerMobilePage, "mobile");
  await capture(sellerMobilePage, path.join(zyDir, "zyphonote-seller-profile-mobile.png"));
  await sellerMobile.close();
}

async function buildManifest() {
  const folders = {
    sandbox: sandboxDir,
    candoitall: candoDir,
    zyphonote: zyDir
  };
  const manifest = {};
  for (const [key, folder] of Object.entries(folders)) {
    manifest[key] = (await fs.readdir(folder))
      .filter(file => file.toLowerCase().endsWith(".png"))
      .sort();
  }

  await fs.writeFile(path.join(proofRoot, "screenshots", "manifest.json"), JSON.stringify(manifest, null, 2));
  console.log(JSON.stringify(manifest, null, 2));
}

(async () => {
  await ensureDirs();
  const browser = await chromium.launch({ headless: true });
  try {
    await captureSandbox(browser);
    await captureCanDoItAll(browser);
    await captureZyphonote(browser);
    await buildManifest();
  } finally {
    await browser.close();
  }
})();