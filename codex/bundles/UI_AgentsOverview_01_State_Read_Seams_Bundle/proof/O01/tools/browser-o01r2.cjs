const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const { chromium } = require(process.env.OV_PLAYWRIGHT);
const root = process.cwd();
const out = path.join(root, '.mcp-state/overview-execution/browser-o01r2');
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
 const metric = () => page.getByTestId('agents-overview-metric-agents').innerText();
 const hr = page.getByTestId('agents-hr-agent-open-header');
 const setMode = async mode => assert((await fetch(control + '/fixture/mode/' + mode, { method: 'POST' })).ok);
 async function capture(mode) {
  const body = await page.locator('body').innerText();
  assert(!body.includes('Controlled private'), 'No private infrastructure text');
  await page.screenshot({ path: path.join(out, mode + '.jpeg'), type: 'jpeg', quality: 80 });
  fs.writeFileSync(path.join(out, mode + '-dom.txt'), body);
  result.scenarios.push({ mode, hrDisabled: await hr.isDisabled(), charts: await page.locator('.apexcharts-canvas svg').count(), metric: await metric(),
   geometry: await page.getByTestId('agents-overview-dashboard').evaluate(e => ({ width: e.getBoundingClientRect().width, scrollWidth: e.scrollWidth, viewport: innerWidth })) });
 }
 async function ready() {
  await until(async () => (await metric()).includes('42') && !await hr.isDisabled(), 'summary and independent HR ready');
  await until(async () => await page.locator('.apexcharts-canvas svg').count() >= 2, 'real settled charts');
 }
 for (const mode of ['Normal', 'Long', 'Partial', 'Empty', 'HoldOverview', 'OverviewFailure', 'UsageFailure', 'HeaderFailure', 'BoundFailure']) {
  await setMode(mode);
  await page.goto(origin + '/agents');
  await page.getByTestId('agents-overview-dashboard').waitFor({ timeout: 90000 });
  if (mode !== 'HeaderFailure') await until(async () => !await hr.isDisabled(), 'independent header ready');
  if (mode === 'HoldOverview') {
   assert((await metric()).includes('...'));
   await until(async () => await page.locator('.apexcharts-canvas svg').count() >= 2, 'usage independent of pending Overview');
  } else if (mode === 'OverviewFailure') {
   await page.getByTestId('agents-overview-load-error').waitFor();
   assert((await metric()).includes('\u2014'));
   await until(async () => await page.locator('.apexcharts-canvas svg').count() >= 2, 'usage independent of failed Overview');
  } else {
   await until(async () => !(await metric()).includes('...'), 'settled summary');
   if (mode === 'UsageFailure') {
    await page.getByTestId('agents-overview-usage-error').waitFor();
    assert((await metric()).includes('42'));
    assert(await page.getByTestId('agents-overview-open-provider-usage').isDisabled());
   } else if (mode !== 'Empty') {
    await until(async () => await page.locator('.apexcharts-canvas svg').count() >= 2, 'real chart settlement');
   }
  }
  if (['HeaderFailure','BoundFailure'].includes(mode)) await page.getByTestId('agents-header-retry').waitFor();
  if (mode === 'HeaderFailure') assert(await hr.isDisabled());
  if (mode === 'Partial') await page.getByTestId('agents-overview-usage-partial').waitFor();
  await capture(mode);
  if (mode === 'HoldOverview') await fetch(control + '/fixture/release', { method: 'POST' });
 }
 await setMode('Normal');
 await page.goto(origin + '/agents');
 await ready();
 for (const lane of ['Overview','Usage']) {
  await setMode(lane + 'Failure');
  await page.getByTestId(lane === 'Overview' ? 'agents-overview-retry' : 'agents-overview-usage-retry').click();
  await page.getByTestId(lane === 'Overview' ? 'agents-overview-stale' : 'agents-overview-usage-stale').waitFor();
  assert((await metric()).includes('42'));
  assert(!await hr.isDisabled());
  if (lane === 'Usage') assert(await page.locator('.apexcharts-canvas svg').count() >= 2);
  await capture(lane + 'RefreshFailure');
  await setMode('Normal');
  await page.getByTestId(lane === 'Overview' ? 'agents-overview-retry' : 'agents-overview-usage-retry').click();
  await page.getByTestId(lane === 'Overview' ? 'agents-overview-stale' : 'agents-overview-usage-stale').waitFor({ state: 'hidden' });
  await ready();
 }
 await setMode('UsageFailure');
 await page.getByTestId('agents-overview-usage-scope').getByRole('button', { name: 'Chats', exact: true }).click();
 await page.getByTestId('agents-overview-usage-error').waitFor();
 assert(new URL(page.url()).searchParams.get('usageScope') === 'simple-chats');
 assert(await page.getByTestId('agents-overview-open-provider-usage').isDisabled());
 assert(!await page.getByTestId('agents-overview-top-consumers').innerText().then(text => text.includes('Agent consumer')));
 await capture('RequestedScopeFailure');
 await setMode('Normal');
 await page.getByTestId('agents-overview-usage-retry').click();
 await page.getByTestId('agents-overview-usage-error').waitFor({ state: 'hidden' });
 await ready();
 assert(!await page.getByTestId('agents-overview-open-provider-usage').isDisabled());
 await capture('RequestedScopeRecovered');
 result.outcome = 'PASS: independent read, stale refresh, and requested-scope recovery through actual Web.';
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
