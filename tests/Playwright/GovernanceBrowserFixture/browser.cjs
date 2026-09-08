const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { spawn, execFileSync } = require('node:child_process');
const { chromium } = require(process.env.GOV_PLAYWRIGHT);
const root = path.resolve(process.argv[2]);
const mode = process.argv[3] || 'web';
assert(['web', 'Parity', 'Fast'].includes(mode));
const raw = path.join(root, '.artifacts/governance-final');
const captures = path.join(raw, 'screenshots');
fs.mkdirSync(captures, { recursive: true });
const base = 'http://127.0.0.1:' + (mode === 'web' ? 5285 : mode === 'Parity' ? 5395 : 5396);
const control = 'http://127.0.0.1:17315';
const agentA = '83cbd161-4bcb-45f9-9cb7-130000000001';
const agentB = '83cbd161-4bcb-45f9-9cb7-130000000002';
const denied = 'governance-denied-payload';
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
const result = { mode, viewport: { width: 1600, height: 1000 }, scenarios: [], errors: [], screenshots: [] };
let child;
let browser;
let page;
let circuitFrames = 0;
async function until(test, label, timeout = 120000) {
    const deadline = Date.now() + timeout;
    while (Date.now() < deadline) {
        try {
            if (await test()) {
                return;
            }
        } catch {}
        assert(child.exitCode === null, 'Owned browser host exited during ' + label);
        await pause(150);
    }
    throw Error('Timed out: ' + label);
}
async function profile() {
    const proceed = page.getByRole('button', { name: 'Continue', exact: true });
    if (await proceed.isVisible().catch(() => false)) {
        await proceed.click();
    }
}
async function title(expected) {
    await until(async () => {
        await profile();
        return (await page.getByTestId('agents-governance-detail-title').innerText()) === expected;
    }, 'detail ' + expected);
}
async function safe(name) {
    const text = await page.locator('body').innerText();
    assert(!text.includes(denied), 'A denied value was rendered.');
    assert.equal(await page.locator('#governance-injected').count(), 0);
    assert.equal(await page.locator('#sandbox-governance-injected').count(), 0);
    const refresh = page.getByRole('button', { name: 'Refresh', exact: true });
    const bounds = await refresh.evaluate(button => {
        const range = document.createRange();
        range.selectNodeContents(button);
        const label = range.getBoundingClientRect();
        const control = button.getBoundingClientRect();
        return { labelLeft: label.left, labelRight: label.right, controlLeft: control.left, controlRight: control.right };
    });
    assert(bounds.labelLeft >= bounds.controlLeft && bounds.labelRight <= bounds.controlRight, 'Refresh text must fit inside its button');
    result.scenarios.push(name);
}
async function capture(name) {
    const file = name + '.jpeg';
    await page.screenshot({ path: path.join(captures, file), type: 'jpeg', quality: 80 });
    result.screenshots.push(file);
}
async function setMode(value) {
    assert((await fetch(control + '/fixture/mode/' + value, { method: 'POST' })).ok);
}
async function counters() {
    return (await fetch(control + '/fixture/state')).json();
}
async function release() {
    assert((await fetch(control + '/fixture/release', { method: 'POST' })).ok);
}
async function webScenarios() {
    await page.goto(base + '/agents?tab=governance&agentId=' + agentA);
    await until(async () => circuitFrames > 1 && (await counters()).detailReads >= 2, 'interactive circuit and canonical read');
    await title('Governance run 1');
    assert.equal(await page.getByTestId('agents-governance-run-item').count(), 2);
    const filter = page.getByLabel('Agent filter', { exact: true });
    result.filterDiagnostic = await filter.evaluate((element, expected) => ({ value: element.value, valueAttribute: element.getAttribute('value'), expectedPresent: Array.from(element.options).some(option => option.value === expected), selected: Array.from(element.selectedOptions).map(option => ({ value: option.value, text: option.textContent })) }), agentA);
    assert.equal(await filter.inputValue(), agentA);
    for (const label of ['Approvals', 'Artifacts', 'Checkpoints', 'Tool receipts', 'Live execution timeline', 'Metrics']) {
        assert((await page.getByTestId('agents-governance-panel').innerText()).toLowerCase().includes(label.toLowerCase()), 'Missing Governance section: ' + label);
    }
    assert((await page.getByTestId('agents-governance-panel').innerText()).includes('2026-03-29 01:30:00 UTC'));
    assert((await page.getByRole('button', { name: 'Refresh', exact: true }).boundingBox()).height < 60, 'Refresh label must stay on one line');
    await safe('real registration, canonical data, all sections, explicit UTC');
    await capture('web-normal');
    await page.getByTestId('agents-governance-run-item').nth(1).press('Enter');
    await title('Governance run 2');
    assert((await page.locator('[aria-current="true"]').innerText()).includes('Selected execution run'));
    await safe('keyboard selection and accessible current state');
    await setMode('HoldDetail');
    await page.getByTestId('agents-governance-run-item').nth(0).click();
    await page.getByTestId('agents-governance-detail-loading').waitFor();
    assert.equal(await page.getByTestId('agents-governance-run-item').count(), 2);
    await setMode('Normal');
    await page.getByTestId('agents-governance-run-item').nth(1).click();
    await title('Governance run 2');
    assert((await counters()).ownerCanceled);
    await release();
    await pause(250);
    await title('Governance run 2');
    await safe('noncooperative R1 completion cannot replace R2');
    await setMode('HoldList');
    await page.getByRole('button', { name: 'Refresh', exact: true }).click();
    await until(async () => (await page.getByTestId('agents-governance-panel').innerText()).includes('Loading execution runs'), 'pending list');
    await filter.selectOption(agentB);
    await title('Governance run B');
    await setMode('Normal');
    await release();
    await pause(250);
    await title('Governance run B');
    await safe('noncooperative A list completion cannot replace B');
    await filter.selectOption(agentA);
    await title('Governance run 1');
    await page.getByTestId('agents-governance-run-item').nth(1).click();
    await title('Governance run 2');
    await page.getByRole('button', { name: 'Refresh', exact: true }).click();
    await title('Governance run 2');
    await safe('manual selection preserved across refresh');
    await setMode('FailDetail');
    await page.getByTestId('agents-governance-run-item').nth(0).click();
    await page.getByTestId('agents-governance-detail-error').waitFor();
    assert.equal(await page.getByTestId('agents-governance-run-item').count(), 2);
    await title('Selected execution details unavailable');
    await safe('bounded detail failure with accepted list');
    await capture('web-detail-failure');
    const before = await counters();
    await setMode('Normal');
    await page.getByTestId('agents-governance-detail-retry').click();
    await title('Governance run 1');
    const after = await counters();
    assert.equal(after.catalogReads, before.catalogReads);
    assert.equal(after.listReads, before.listReads);
    assert.equal(after.detailReads, before.detailReads + 1);
    await safe('detail-only retry');
    await setMode('FailList');
    await page.getByRole('button', { name: 'Refresh', exact: true }).click();
    await page.getByTestId('agents-governance-list-stale').waitFor();
    assert.equal(await page.getByTestId('agents-governance-run-item').count(), 2);
    await title('Governance run 1');
    await setMode('Normal');
    await page.getByTestId('agents-governance-list-retry').click();
    await page.getByTestId('agents-governance-list-stale').waitFor({ state: 'hidden' });
    await safe('stale list and scoped recovery');
    await page.getByTestId('agents-governance-run-item').nth(1).click();
    await title('Governance run 2');
    await setMode('Removed');
    await page.getByRole('button', { name: 'Refresh', exact: true }).click();
    await page.getByTestId('agents-governance-detail-unavailable').waitFor();
    assert.equal(await page.getByTestId('agents-governance-run-item').count(), 1);
    await setMode('Normal');
    await page.getByRole('button', { name: 'Refresh', exact: true }).click();
    await title('Governance run 2');
    await safe('removed selected run stays explicit and can reappear');
    await setMode('Long');
    await page.getByRole('button', { name: 'Refresh', exact: true }).click();
    await until(async () => (await page.getByTestId('agents-governance-detail-title').innerText()).includes('<img'), 'encoded adversarial title');
    assert((await page.getByTestId('agents-governance-detail-title').innerText()).length <= 160);
    await safe('bounded adversarial content');
    await capture('web-long');
    await page.setViewportSize({ width: 768, height: 1024 });
    await pause(250);
    assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Responsive document overflow');
    await page.getByTestId('agents-governance-detail-title').scrollIntoViewIfNeeded();
    assert(await page.getByTestId('agents-governance-detail-title').isVisible());
    await safe('responsive layout at 768 pixels');
    await capture('web-responsive');
}
async function sandboxScenarios() {
    await page.goto(base + '/agents?specimen=governance&scenario=selected-agent&layout=matched');
    await until(() => circuitFrames > 1, 'interactive sandbox circuit');
    await title('Governance run 1');
    const scenario = page.getByTestId('sandbox-governance-scenario');
    const choices = await scenario.locator('option').evaluateAll(options => options.map(option => option.value));
    for (const choice of choices) {
        await scenario.selectOption(choice);
        await until(async () => await page.getByTestId('sandbox-governance-specimen').getAttribute('data-scenario') === choice, 'accepted sandbox scenario');
        await safe('sandbox ' + choice);
    }
    await scenario.selectOption('selected-agent');
    await until(async () => await page.getByTestId('sandbox-governance-specimen').getAttribute('data-scenario') === 'selected-agent', 'accepted selected-agent scenario');
    await title('Governance run 1');
    await page.getByTestId('agents-governance-run-item').nth(1).click();
    await title('Governance run 2');
    assert((await page.getByTestId('sandbox-intent').innerText()).includes('SelectRun'));
    await page.getByLabel('Agent filter', { exact: true }).selectOption('');
    await until(async () => (await page.getByTestId('sandbox-intent').innerText()).includes('SelectAgent'), 'accepted SelectAgent intent');
    assert.equal(await page.getByTestId('agents-governance-run-item').count(), 3);
    await safe('controlled sample selection and intent log');
    await scenario.selectOption('detail-failure');
    await page.getByTestId('agents-governance-detail-error').waitFor();
    await page.getByTestId('agents-governance-detail-retry').click();
    await title('Governance run 1');
    await safe('sample detail retry');
    await scenario.selectOption('long-text');
    await until(async () => await page.getByTestId('sandbox-governance-specimen').getAttribute('data-scenario') === 'long-text', 'accepted long-text scenario');
    await page.setViewportSize({ width: 768, height: 1024 });
    await pause(250);
    assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Sandbox overflow');
    await safe('responsive long-content sample');
    await page.setViewportSize(result.viewport);
    await scenario.selectOption('selected-agent');
    await until(async () => await page.getByTestId('sandbox-governance-specimen').getAttribute('data-scenario') === 'selected-agent', 'accepted selected-agent scenario');
    await title('Governance run 1');
    await capture('sandbox-' + mode.toLowerCase());
    const state = await (await fetch(base + '/_dev/runtime')).json();
    assert.equal(state.assetMode, mode);
}
(async () => {
    const log = fs.createWriteStream(path.join(raw, 'browser-' + mode + '.log'));
    const args = mode === 'web' ? [path.join(root, 'tests/Playwright/GovernanceBrowserFixture/bin/Release/net10.0/GovernanceBrowserFixture.dll'), 'serve', root]
        : [path.join(root, 'src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/bin', mode, 'Release/net10.0/CanDoItAll.AgentFramework.UiSandbox.dll')];
    child = spawn('dotnet', args, { cwd: mode === 'web' ? root : path.join(root, 'src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox'),
        env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', DOTNET_ENVIRONMENT: 'Development', ASPNETCORE_URLS: base, CatalogAssetMode: mode },
        windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
    child.stdout.pipe(log, { end: false });
    child.stderr.pipe(log, { end: false });
    await until(async () => (await fetch(base + '/agents')).ok, 'host ready');
    browser = await chromium.launch({ headless: true });
    result.browser = browser.version();
    page = await browser.newPage({ viewport: result.viewport });
    page.on('pageerror', error => result.errors.push(error.message));
    page.on('websocket', socket => {
        if (new URL(socket.url()).pathname.endsWith('/_blazor')) {
            socket.on('framereceived', () => circuitFrames++);
        }
    });
    if (mode === 'web') {
        await webScenarios();
    } else {
        await sandboxScenarios();
    }
    assert.equal(result.errors.length, 0, 'Unexpected browser errors');
})().catch(error => {
    result.failure = error.message;
    process.exitCode = 1;
}).finally(async () => {
    if (browser) {
        await browser.close();
    }
    if (mode === 'web' && child?.exitCode === null) {
        await fetch(control + '/fixture/stop', { method: 'POST' }).catch(() => {});
        await pause(1500);
    }
    if (child?.exitCode === null) {
        if (process.platform === 'win32') {
            execFileSync('taskkill', ['/PID', String(child.pid), '/T', '/F'], { windowsHide: true, stdio: 'ignore' });
        } else {
            child.kill('SIGTERM');
        }
    }
    result.circuitFrames = circuitFrames;
    fs.writeFileSync(path.join(raw, 'browser-' + mode + '.json'), JSON.stringify(result, null, 2));
    console.log(JSON.stringify(result));
});
