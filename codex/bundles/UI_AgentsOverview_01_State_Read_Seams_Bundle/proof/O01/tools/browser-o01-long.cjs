const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const { chromium } = require(process.env.OV_PLAYWRIGHT);
const root = process.cwd();
const out = path.join(root, '.mcp-state/overview-execution/browser-o01-long');
fs.mkdirSync(out, { recursive: true });
const log = fs.createWriteStream(path.join(out, 'host.txt'));
const child = spawn('dotnet', [path.join(root, '.mcp-state/overview-browser-fixture/bin/Release/net10.0/OverviewBrowser.dll'), root], { cwd: root, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
child.stdout.pipe(log, { end: false });
child.stderr.pipe(log, { end: false });
const control = 'http://127.0.0.1:17305';
const origin = 'http://127.0.0.1:5275';
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
const result = { utc: new Date().toISOString(), ownedPid: child.pid, viewport: { width: 1600, height: 1000 }, scenarios: [], errors: [] };
async function until(test, label, timeout = 120000) {
 const end = Date.now() + timeout;
 while (Date.now() < end) {
  try { if (await test()) return; } catch { }
  if (child.exitCode !== null) throw Error('Fixture exited: ' + label);
  await pause(200);
 }
 throw Error('Timeout: ' + label);
}
let browser;
(async () => {
 await until(async () => (await fetch(origin + '/agents')).ok, 'Web readiness');
 browser = await chromium.launch({ headless: true });
 result.browser = browser.version();
 const page = await browser.newPage({ viewport: result.viewport });
 page.on('pageerror', error => result.errors.push(error.message));
 await page.goto(origin + '/agents');
 const proceed = page.getByRole('button', { name: 'Continue', exact: true });
 await proceed.waitFor({ timeout: 90000 });
 await proceed.click();
 await proceed.waitFor({ state: 'hidden' });
 for (const mode of ['Long']) {
  assert((await fetch(control + '/fixture/mode/' + mode, { method: 'POST' })).ok);
  await page.goto(origin + '/agents');
  await page.getByTestId('agents-overview-dashboard').waitFor({ timeout: 90000 });
  if (mode === 'HoldOverview') {
   await until(async () => (await page.getByTestId('agents-overview-metric-agents').innerText()).includes('...'), 'held Overview');
  } else if (['OverviewFailure','UsageFailure','BoundFailure'].includes(mode)) {
   await page.getByTestId('agents-overview-load-error').waitFor();
  } else {
   await until(async () => !(await page.getByTestId('agents-overview-metric-agents').innerText()).includes('...'), 'settled summary');
   if (mode !== 'Empty') {
    await until(async () => await page.locator('.apexcharts-canvas svg').count() >= 2, 'real chart settlement');
   }
  }
  assert((await page.getByTestId('agents-overview-top-consumers').innerText()).includes('Long'));
  assert((await page.getByTestId('agents-overview-team-shortcut').first().getAttribute('title')).includes('interdisciplinary'));
  await page.screenshot({ path: path.join(out, mode + '.jpeg'), type: 'jpeg', quality: 80 });
  fs.writeFileSync(path.join(out, mode + '-dom.txt'), await page.locator('body').innerText());
  result.scenarios.push({ mode, hrDisabled: await page.getByTestId('agents-hr-agent-open-header').isDisabled(),
   charts: await page.locator('.apexcharts-canvas svg').count(), metric: await page.getByTestId('agents-overview-metric-agents').innerText(),
   geometry: await page.locator('[data-testid="agents-overview-dashboard"]').evaluate(e => ({ width: e.getBoundingClientRect().width, scrollWidth: e.scrollWidth, viewport: innerWidth })) });
  if (mode === 'HoldOverview') {
   await fetch(control + '/fixture/release', { method: 'POST' });
  }
 }
 result.outcome = 'PASS: actual long consumer/provider/team labels rendered; current chart legend limitation retained for O02.';
})().catch(error => { result.failure = error.stack; process.exitCode = 1; }).finally(async () => {
 if (browser) await browser.close();
 try { await fetch(control + '/fixture/stop', { method: 'POST' }); } catch { }
 const end = Date.now() + 15000;
 while (child.exitCode === null && Date.now() < end) await pause(100);
 if (child.exitCode === null) child.kill();
 result.ownedExit = child.exitCode;
 result.finishedUtc = new Date().toISOString();
 fs.writeFileSync(path.join(out, 'summary.json'), JSON.stringify(result, null, 2));
 console.log(JSON.stringify({ scenarios: result.scenarios, failure: result.failure, ownedExit: result.ownedExit }));
 log.end();
});
