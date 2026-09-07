const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { spawn, execFileSync } = require('node:child_process');
const { chromium } = require(process.env.CAPA_PLAYWRIGHT);
const root = process.cwd();
const out = path.join(root, process.argv[2] || '.mcp-state/capa03/browser-final-consumers-green');
fs.mkdirSync(out, { recursive: true });
const log = fs.createWriteStream(path.join(out, 'host.txt'));
const child = spawn('dotnet', [path.join(root, '.mcp-state/capa03-browser-fixture/bin/Release/net10.0/CapabilitiesBrowser.dll'), root], { cwd: root, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
child.stdout.pipe(log, { end: false });
child.stderr.pipe(log, { end: false });
const control = 'http://127.0.0.1:17301';
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
async function state() {
 const response = await fetch(control + '/fixture/state');
 assert(response.ok);
 return response.json();
}
async function until(check, label, timeout = 120000) {
 const end = Date.now() + timeout;
 while (Date.now() < end) {
  try { if (await check()) return; } catch { }
  if (child.exitCode !== null) throw new Error('Owned fixture exited before ' + label);
  await pause(200);
 }
 throw new Error('Timed out: ' + label);
}
let browser;
let page;
const errors = [];
const evidence = { ownedPid: child.pid, viewport: { width: 1600, height: 1000 }, reason: 'Final shared-list CSS in both real consumers', geometry: [] };
async function shot(name) {
 await page.screenshot({ path: path.join(out, name + '.jpeg'), type: 'jpeg', quality: 80 });
 fs.writeFileSync(path.join(out, name + '-dom.txt'), await page.locator('body').innerText());
}
(async () => {
 await until(async () => Boolean((await state()).alphaId), 'fixture readiness');
 evidence.initial = await state();
 browser = await chromium.launch({ headless: true });
 evidence.browserVersion = browser.version();
 page = await browser.newPage({ viewport: evidence.viewport });
 page.on('pageerror', e => errors.push(e.message));
 await page.goto('http://127.0.0.1:5273/agents?tab=capabilities&agentId=' + evidence.initial.alphaId);
 const proceed = page.getByRole('button', { name: 'Continue', exact: true });
 await proceed.waitFor({ timeout: 90000 });
 await proceed.click();
 await proceed.waitFor({ state: 'hidden' });
 await page.getByTestId('agents-capability-search').fill('CAPA03 Inline inspection');
 await until(async () => await page.getByTestId('agents-capability-card').count() === 1, 'surface filter');
 await shot('surface');
 await page.goto('http://127.0.0.1:5273/agents?tab=agents&agentId=' + evidence.initial.alphaId);
 const dialog = page.getByRole('dialog');
 await dialog.waitFor({ timeout: 90000 });
 await dialog.getByRole('tab', { name: 'Capabilities', exact: true }).click();
 await dialog.getByTestId('agents-details-capability-search').fill('CAPA03 Inline inspection');
 const card = dialog.getByTestId('agents-details-capability-card');
 await until(async () => await card.count() === 1, 'details filter');
 const content = card.locator('.agent-capability-list__content');
 const cardBox = await card.boundingBox();
 const contentBox = await content.boundingBox();
 assert(contentBox.width > 0 && contentBox.width <= cardBox.width);
 assert(contentBox.x >= cardBox.x && contentBox.x + contentBox.width <= cardBox.x + cardBox.width + 1);
 evidence.geometry.push({ consumer: 'AgentDetailsDialog', card: cardBox, content: contentBox });
 assert.equal(await dialog.getByTestId('agents-details-capability-verify').isDisabled(), true);
 await dialog.getByTestId('agents-details-capability-toggle').click();
 await until(async () => (await dialog.getByTestId('agents-details-capability-toggle').innerText()).trim() === 'Remove', 'existing-agent assignment display');
 await until(async () => (await state()).assigned && !(await dialog.getByTestId('agents-details-capability-verify').isDisabled()), 'existing-agent save and reconciliation');
 await shot('agent-details-capabilities');
 await page.keyboard.press('Escape');
 await dialog.waitFor({ state: 'hidden' });
 evidence.final = await state();
 assert.equal(evidence.final.assigned, true);
 assert.equal(evidence.final.diagnosticCalls, 0);
 assert.equal(evidence.final.curatorCalls, 0);
 assert.deepEqual(errors, []);
 evidence.status = 'PASS';
})().catch(async error => {
 evidence.status = 'FAIL';
 evidence.error = error.message;
 if (page) await shot('failure').catch(() => {});
 process.exitCode = 1;
}).finally(async () => {
 if (browser) await browser.close();
 await fetch(control + '/fixture/stop', { method: 'POST' }).catch(() => {});
 const end = Date.now() + 30000;
 while (child.exitCode === null && Date.now() < end) await pause(100);
 if (child.exitCode === null) {
  execFileSync('taskkill', ['/PID', String(child.pid), '/T', '/F']);
  evidence.forcedOwnedStop = true;
 } else evidence.ownedExit = child.exitCode;
 evidence.pageErrors = errors;
 log.end();
 fs.writeFileSync(path.join(out, 'summary.json'), JSON.stringify(evidence, null, 2));
 console.log(JSON.stringify(evidence));
});
