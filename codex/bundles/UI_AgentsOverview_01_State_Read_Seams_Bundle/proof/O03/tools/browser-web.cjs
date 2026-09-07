const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const { chromium } = require(process.env.OV_PLAYWRIGHT);
const root = process.cwd();
const out = path.join(root, '.mcp-state/overview03/browser-web-r2');
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
  await page.waitForTimeout(350);
  await page.screenshot({ path: path.join(out, mode + '.jpeg'), type: 'jpeg', quality: 80 });
  fs.writeFileSync(path.join(out, mode + '-dom.txt'), body);
  result.scenarios.push({ mode, hrDisabled: await hr.isDisabled(), charts: await page.locator('.apexcharts-canvas svg.apexcharts-svg').count(), metric: await metric(),
   geometry: await page.getByTestId('agents-overview-dashboard').evaluate(e => ({ width: e.getBoundingClientRect().width, scrollWidth: e.scrollWidth, viewport: innerWidth })) });
 }
 async function ready() {
  await until(async () => (await metric()).includes('42') && !await hr.isDisabled(), 'summary and independent HR ready');
  await until(async () => await page.locator('.apexcharts-canvas svg.apexcharts-svg').count() >= 2, 'real settled charts');
 }
 for (const mode of ['Normal', 'Long', 'Partial', 'Empty', 'HoldOverview', 'OverviewFailure', 'UsageFailure', 'HeaderFailure', 'BoundFailure']) {
  await setMode(mode);
  await page.goto(origin + '/agents');
  await page.getByTestId('agents-overview-dashboard').waitFor({ timeout: 90000 });
  if (mode !== 'HeaderFailure') await until(async () => !await hr.isDisabled(), 'independent header ready');
  if (mode === 'HoldOverview') {
   assert((await metric()).includes('...'));
   await until(async () => await page.locator('.apexcharts-canvas svg.apexcharts-svg').count() >= 2, 'usage independent of pending Overview');
  } else if (mode === 'OverviewFailure') {
   await page.getByTestId('agents-overview-load-error').waitFor();
   assert((await metric()).includes('\u2014'));
   await until(async () => await page.locator('.apexcharts-canvas svg.apexcharts-svg').count() >= 2, 'usage independent of failed Overview');
  } else {
   await until(async () => !(await metric()).includes('...'), 'settled summary');
   if (mode === 'UsageFailure') {
    await page.getByTestId('agents-overview-usage-error').waitFor();
    assert((await metric()).includes('42'));
    assert(await page.getByTestId('agents-overview-open-provider-usage').isDisabled());
   } else if (mode !== 'Empty') {
    await until(async () => await page.locator('.apexcharts-canvas svg.apexcharts-svg').count() >= 2, 'real chart settlement');
   }
  }
  if (['HeaderFailure','BoundFailure'].includes(mode)) await page.getByTestId('agents-header-retry').waitFor();
  if (mode === 'HeaderFailure') assert(await hr.isDisabled());
  if (mode === 'Partial') await page.getByTestId('agents-overview-usage-partial').waitFor();
  await capture(mode);
  if (mode === 'Long') {
   await page.getByTestId('agents-overview-open-model-usage').click();
   const shell = page.getByTestId('model-usage-dialog-shell');
   await until(async () => await shell.locator('svg.apexcharts-svg').count() > 0, 'long model real chart');
   await until(async () => (await shell.innerText()).includes('Long model for multilingual research'), 'long model row');
   await capture('LongModelDialog');
   await shell.getByRole('button', { name: 'Close', exact: true }).first().click();
   await shell.waitFor({ state: 'hidden' });
  }
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
  if (lane === 'Usage') assert(await page.locator('.apexcharts-canvas svg.apexcharts-svg').count() >= 2);
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
 await until(async () => (await page.getByTestId('agents-overview-usage-scope').getByRole('button', { name: 'Chats', exact: true }).getAttribute('class')).includes('bg-slate-900'), 'controlled selected Chats tab');
 await capture('RequestedScopeFailure');
 await setMode('Normal');
 await page.getByTestId('agents-overview-usage-retry').click();
 await page.getByTestId('agents-overview-usage-error').waitFor({ state: 'hidden' });
 await ready();
 assert(!await page.getByTestId('agents-overview-open-provider-usage').isDisabled());
 await capture('RequestedScopeRecovered');
 // Current fixture owns real team identities and actual Web composition.
 await setMode('Normal');
 await page.goto(origin + '/agents');
 await ready();
 for (const [kind, openId, shellId, contentId] of [
  ['Consumer','agents-overview-open-agent-usage','agents-usage-dialog-shell','agents-usage-dialog'],
  ['Provider','agents-overview-open-provider-usage','provider-usage-dialog-shell','provider-usage-dialog'],
  ['Model','agents-overview-open-model-usage','model-usage-dialog-shell','model-usage-dialog']]) {
  await page.getByTestId(openId).click();
  const shell = page.getByTestId(shellId);
  await shell.getByTestId(contentId).waitFor();
  await until(async () => await shell.locator('.apexcharts-canvas svg.apexcharts-svg').count() > 0, kind + ' actual chart');
  await capture(kind + 'Dialog');
  await shell.getByRole('button', { name: 'Close', exact: true }).first().click();
  await shell.waitFor({ state: 'hidden' });
  assert(!await page.getByTestId(openId).isDisabled());
  await setMode('HoldUsage');
  await page.getByTestId(openId).click();
  await shell.waitFor();
  await until(async () => (await fetch(control + '/fixture/state').then(r=>r.json())).usageReads > 0, 'dialog read started');
  await capture(kind + 'Pending');
  await shell.getByRole('button', { name: 'Close', exact: true }).first().click();
  await shell.waitFor({ state: 'hidden' });
  await until(async () => (await fetch(control + '/fixture/state').then(r=>r.json())).cancelled, kind + ' read cancellation');
  await setMode('Normal');
  await fetch(control + '/fixture/release', { method: 'POST' });
  assert(!await page.getByTestId(openId).isDisabled());
 }
 // A failed aggregate must not disable HR; opening a chat does not send a provider request.
 await setMode('OverviewFailure');
 await page.getByTestId('agents-overview-retry').click();
 await page.getByTestId('agents-overview-stale').waitFor();
 await hr.click();
 await until(async () => (await page.getByTestId('floating-agent-chat-host').innerText()).includes('HR Agent'), 'real independent HR chat');
 await capture('HrChatAfterOverviewFailure');
 await setMode('Normal');
 await page.goto(origin + '/agents');
 await ready();
 await page.getByTestId('agents-shell-feed-defaults').click();
 await page.getByTestId('agents-feed-defaults-confirmation-content').waitFor();
 await capture('DefaultsConfirmation');
 await page.getByTestId('agents-feed-defaults-cancel').click();
 await page.getByTestId('agents-feed-defaults-confirmation-content').waitFor({ state: 'hidden' });
 await page.getByTestId('agents-shell-feed-defaults').click();
 await page.getByTestId('agents-feed-defaults-confirm').click();
 await until(async () => (await page.locator('body').innerText()).includes('were synchronized.'), 'real defaults command');
 await ready();
 await capture('DefaultsCompleted');
 const beforeHistory = await fetch(control + '/fixture/state').then(r=>r.json());
 for (const tab of ['Providers','Request history']) {
  await page.getByTestId('agents-shell-tabs').getByRole('button', { name: new RegExp('^' + tab) }).click();
  await page.getByTestId('agents-overview-dashboard').waitFor({ state: 'hidden' });
  assert(!await hr.isDisabled());
  await page.waitForTimeout(600);
  const after = await fetch(control + '/fixture/state').then(r=>r.json());
  assert.equal(after.overviewReads, beforeHistory.overviewReads);
  assert.equal(after.usageReads, beforeHistory.usageReads);
  await page.screenshot({ path: path.join(out, tab.replaceAll(' ','') + '.jpeg'), type: 'jpeg', quality: 80 });
  result.scenarios.push({ mode: tab, headerPreserved: true, aggregateReadsUnchanged: true });
 }
 await page.getByTestId('agents-shell-tabs').getByRole('button', { name: 'Overview', exact: true }).click();
 await ready();
 await page.getByTestId('agents-overview-team-shortcut').first().click();
 await until(async () => new URL(page.url()).searchParams.get('teamId') === '580e7395-0890-43ad-b27e-e19f0369d571', 'real team route');
 await until(async () => (await page.locator('body').innerText()).includes('Product research and synthesis'), 'real selected team');
 assert(!await hr.isDisabled());
 await page.screenshot({ path: path.join(out, 'TeamNavigation.jpeg'), type: 'jpeg', quality: 80 });
 result.scenarios.push({ mode: 'TeamNavigation', target: new URL(page.url()).searchParams.get('teamId') });
 await setMode('Normal');
 await page.goto(origin + '/agents');
 await ready();
 for (const width of [1280, 900]) {
  await page.setViewportSize({ width, height: 1000 });
  await page.waitForTimeout(1000);
  const geometry = await page.getByTestId('agents-overview-dashboard').evaluate(e => ({ width: e.getBoundingClientRect().width, scrollWidth: e.scrollWidth, viewport: innerWidth, columns: getComputedStyle(e.querySelector('.agents-overview-main-grid')).gridTemplateColumns }));
  assert(geometry.scrollWidth <= Math.ceil(geometry.width) + 2, 'Overview has no horizontal overflow');
  await page.getByTestId('agents-overview-provider-bar').scrollIntoViewIfNeeded();
  await page.screenshot({ path: path.join(out, 'Responsive' + width + '.jpeg'), type: 'jpeg', quality: 80 });
  result.scenarios.push({ mode: 'Responsive' + width, geometry });
 }
 assert.deepEqual(result.errors, []);
 result.outcome = 'PASS: current real Web reads, scope, three settled dialogs, cancellation, HR/defaults/team effects and history demand.';
})().catch(async error => { result.failure = error.stack; process.exitCode = 1; if (browser) { const p = browser.contexts()[0]?.pages()[0]; if (p) { await p.screenshot({path:path.join(out,'failure.jpeg')}); fs.writeFileSync(path.join(out,'failure-dom.txt'), await p.locator('body').innerText()); } } }).finally(async () => {
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
