const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { spawn, execFileSync } = require('node:child_process');
const { chromium } = require(process.env.GOV_PLAYWRIGHT);
const root = path.resolve(process.argv[2]);
const mode = process.argv[3] || 'web';
const feature = process.argv[4] || 'diagnostics';
const raw = path.join(root, '.artifacts/diagnostics-history');
fs.mkdirSync(raw, { recursive: true });
const base = 'http://127.0.0.1:' + (mode === 'web' ? 5285 : mode === 'Parity' ? 5395 : 5396);
const control = 'http://127.0.0.1:17315';
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
const result = { feature, mode, viewport: { width: 1600, height: 1000 }, scenarios: [], errors: [] };
let child, browser, page;
let circuitFrames = 0;
async function until(test, label) {
    const deadline = Date.now() + 45000;
    while (Date.now() < deadline) {
        try {
            if (await test()) {
                return;
            }
        } catch {}
        assert.equal(child.exitCode, null, 'Owned host exited: ' + label);
        await pause(150);
    }
    throw Error('Timed out: ' + label);
}
async function safe(name) {
    const element = page.getByTestId('agents-diagnostics-panel');
    const text = await element.innerText();
    const accessible = await element.ariaSnapshot();
    assert(!text.includes('diagnostics-private-sentinel') && !accessible.includes('diagnostics-private-sentinel'));
    assert.equal(await page.locator('#diagnostics-injected').count(), 0);
    assert.equal(await element.evaluate(e => e.scrollWidth > e.clientWidth + 2), false);
    result.scenarios.push(name);
}
async function diagnostics() {
    if (mode === 'web') {
        await page.goto(base + '/agents');
        await until(async () => circuitFrames > 2, 'interactive shell');
        await page.getByTestId('database-startup-continue').click();
        await until(async () => await page.getByTestId('database-startup-modal').count() === 0, 'profile dialog closed');
        await page.getByRole('button', { name: /^Diagnostics/ }).click();
    } else {
        await page.goto(base + '/agents?specimen=diagnostics');
        await until(async () => circuitFrames > 2, 'interactive sandbox');
    }
    await until(async () => await page.getByTestId('diagnostics-refresh').isEnabled(), 'initial diagnostics ready');
    await safe('initial automatic read');
    const refresh = page.getByTestId('diagnostics-refresh');
    if (mode === 'web') {
        for (const [fault, lane] of [['DashboardFailure', 'dashboard'], ['AgentsFailure', 'agents'], ['RunsFailure', 'runs']]) {
            await fetch(control + '/fixture/diagnostics/' + fault, { method: 'POST' });
            await refresh.click();
            await until(async () => (await page.getByTestId('diagnostics-' + lane + '-state').innerText()).includes('stale'), 'stale ' + lane);
            await safe(fault + ' retains independent lanes');
            await fetch(control + '/fixture/diagnostics/Normal', { method: 'POST' });
            await page.getByRole('button', { name: 'Retry ' + lane, exact: true }).click();
            await until(async () => await page.getByTestId('diagnostics-' + lane + '-state').count() === 0, 'retry ' + lane);
        }
        await fetch(control + '/fixture/diagnostics/Poison', { method: 'POST' });
        await refresh.click();
        await until(async () => (await page.getByTestId('agents-diagnostics-panel').innerText()).includes('<script'), 'real domain poison mapping');
        await safe('domain poison omitted from text and accessibility');
        await fetch(control + '/fixture/diagnostics/Normal', { method: 'POST' });
        await refresh.click();
        await until(async () => await refresh.isEnabled() && !(await page.getByTestId('agents-diagnostics-panel').innerText()).includes('<script'), 'normal refresh');
    } else {
        const select = page.locator('#diagnostics-scenario');
        for (const value of await select.locator('option').evaluateAll(options => options.map(option => option.value))) {
            await select.selectOption(value);
            await safe('sandbox ' + value);
        }
        await select.selectOption('ready');
        await refresh.click();
        await until(async () => (await page.getByTestId('sandbox-intent').innerText()).includes('Refresh'), 'controlled refresh');
    }
    await safe('refresh usable');
    await until(async () => {
        const dismiss = page.getByRole('button', { name: 'Dismiss notification', exact: true });
        if (await dismiss.count()) {
            await dismiss.first().press('Enter', { timeout: 500 }).catch(() => {});
        }
        return await dismiss.count() === 0;
    }, 'owned notifications dismissed');
    await page.screenshot({ path: path.join(raw, 'diagnostics-' + mode + '.png') });
}
async function historySafe(name) {
    const text = await page.locator('body').innerText();
    const accessible = await page.locator('body').ariaSnapshot();
    assert(!text.includes('history-browser-private-sentinel') && !accessible.includes('history-browser-private-sentinel'));
    assert.equal(await page.locator('#history-injected').count(), 0);
    result.scenarios.push(name);
}
async function openWebTab(name) {
    await page.goto(base + '/agents');
    await until(async () => circuitFrames > 2, 'interactive shell');
    await page.getByTestId('database-startup-continue').click();
    await until(async () => await page.getByTestId('database-startup-modal').count() === 0, 'profile dialog closed');
    await page.getByRole('button', { name }).click();
}
async function historyScenarios() {
    if (mode !== 'web') {
        await page.goto(base + '/agents?specimen=history');
        await until(async () => circuitFrames > 2, 'interactive history sandbox');
        const select = page.locator('#history-scenario');
        for (const value of await select.locator('option').evaluateAll(options => options.map(option => option.value))) {
            await select.selectOption(value);
            await historySafe('sandbox ' + value);
            assert.equal(await page.getByTestId('history-content-text').count(), 0);
        }
        await select.selectOption('paged');
        await page.getByTestId('history-next').click();
        await until(async () => (await page.getByTestId('history-results').innerText()).includes('Page 3'), 'next sample page');
        await page.getByTestId('history-previous').click();
        await until(async () => (await page.getByTestId('history-results').innerText()).includes('Page 2'), 'previous sample page');
        await select.selectOption('metadata');
        await page.getByTestId('history-details').click();
        await page.getByTestId('sandbox-history-load-content').waitFor();
        assert.equal(await page.getByTestId('history-content-text').count(), 0);
        await page.getByTestId('sandbox-history-load-content').click();
        await page.getByTestId('history-content-text').first().waitFor();
        assert((await page.getByTestId('history-content-text').first().inputValue()).includes('Synthetic authorized input'));
        await historySafe('explicit synthetic content and paging');
        await page.getByTestId('history-content-close').click();
        await until(async () => await page.getByTestId('history-content-text').count() === 0, 'content closed');
        await page.screenshot({ path: path.join(raw, 'history-' + mode + '.png') });
        return;
    }
    const counters = async () => (await fetch(control + '/fixture/history')).json();
    const fault = async value => assert((await fetch(control + '/fixture/history/' + value, { method: 'POST' })).ok);
    await until(async () => (await counters()).ready, 'canonical synthetic history capture');
    await openWebTab(/^Request history/);
    await page.getByTestId('history-results-surface').waitFor();
    assert.equal((await counters()).searches, 0);
    assert.equal((await counters()).metadataReads, 0);
    assert.equal((await counters()).contentReads, 0);
    const model = (await counters()).model;
    await page.getByTestId('history-model').fill(model);
    await page.getByTestId('history-model').press('Tab');
    await page.getByTestId('history-more-filters').click();
    await page.getByTestId('history-page-size').fill('0');
    await page.getByTestId('history-search').click();
    assert.equal((await counters()).searches, 0);
    await page.getByTestId('history-page-size').fill('10');
    await page.getByTestId('history-search').click();
    await until(async () => await page.getByTestId('history-details').count() === 10, 'first canonical page');
    await historySafe('zero reads on open; invalid query rejected; explicit canonical search');
    await page.getByTestId('history-model').fill('unapplied-model');
    await page.getByTestId('history-model').press('Tab');
    await page.getByTestId('history-draft-warning').waitFor();
    await page.getByTestId('history-next').click();
    await until(async () => await page.getByTestId('history-details').count() === 5, 'applied query next page');
    assert((await page.getByTestId('history-applied').innerText()).includes(model));
    await page.getByTestId('history-previous').click();
    await until(async () => await page.getByTestId('history-details').count() === 10, 'previous page');
    assert.equal((await counters()).contentReads, 0);
    await page.getByTestId('history-details').first().click();
    await page.getByTestId('history-load-content').waitFor();
    assert.equal((await counters()).metadataReads, 1);
    assert.equal((await counters()).contentReads, 0);
    await page.screenshot({ path: path.join(raw, 'history-web-metadata.png') });
    await page.getByTestId('history-load-content').click();
    await page.getByTestId('history-content-text').first().waitFor();
    const input = await page.getByTestId('history-content-text').first().inputValue();
    assert(input.includes('Synthetic authorized input'));
    assert(!input.includes('history-browser-private-sentinel'));
    assert.equal((await counters()).contentReads, 1);
    await historySafe('applied-query paging; explicit metadata; explicit authorized redacted content');
    await page.getByTestId('history-content-close').click();
    await page.getByTestId('history-detail-close').click();
    await until(async () => await page.getByTestId('history-detail-dialog').count() === 0, 'details fully removed');
    await page.getByTestId('history-model').fill(model);
    await page.getByTestId('history-model').press('Tab');
    await fault('HoldSearch');
    await page.getByTestId('history-search').click();
    await page.getByTestId('history-cancel').waitFor();
    await page.getByTestId('history-cancel').click();
    await until(async () => (await page.getByTestId('history-results-surface').innerText()).includes('Search canceled'), 'canceled state');
    await page.getByTestId('history-clear').click();
    await fetch(control + '/fixture/release', { method: 'POST' });
    await until(async () => (await page.getByTestId('history-results-surface').innerText()).includes('History not requested'), 'clear defeats late response');
    assert.equal(await page.getByTestId('history-results').count(), 0);
    await fault('FailSearch');
    await page.getByTestId('history-search').click();
    await page.getByTestId('history-error').waitFor();
    await historySafe('cancel, clear, late completion and sanitized error');
    await fault('Normal');
    await page.getByTestId('history-search').click();
    await until(async () => await page.getByTestId('history-details').count() === 10, 'recovered search');
    await fault('HoldMetadata');
    await page.getByTestId('history-details').first().click();
    await page.getByTestId('history-detail-close').click();
    await fetch(control + '/fixture/release', { method: 'POST' });
    await until(async () => await page.getByTestId('history-detail-dialog').count() === 0, 'closed metadata cannot reappear');
    await historySafe('close during metadata read');
}
async function governanceSmoke() {
    if (mode === 'web') {
        await openWebTab(/^Governance/);
    } else {
        await page.goto(base + '/agents?specimen=governance&scenario=selected-agent');
        await until(async () => circuitFrames > 2, 'interactive Governance sandbox');
    }
    await until(async () => await page.getByTestId('agents-governance-run-item').count() >= 2, 'accepted Governance list');
    assert((await page.getByTestId('agents-governance-panel').innerText()).includes('UTC'));
    await page.getByTestId('agents-governance-run-item').nth(1).click();
    await until(async () => await page.getByTestId('agents-governance-run-item').nth(1).evaluate(element => element.closest('[aria-current]')?.getAttribute('aria-current') === 'true'), 'Governance run selection');
    result.scenarios.push('accepted predecessor renders and selects a run');
}
(async () => {
    const log = fs.createWriteStream(path.join(raw, 'browser-runtime.log'), { flags: 'a' });
    const args = mode === 'web' ? [path.join(root, 'tests/Playwright/GovernanceBrowserFixture/bin/Release/net10.0/GovernanceBrowserFixture.dll'), feature === 'history' ? 'serve-history' : 'serve', root]
        : [path.join(root, 'src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/bin', mode, 'Release/net10.0/CanDoItAll.AgentFramework.UiSandbox.dll')];
    child = spawn('dotnet', args, { cwd: mode === 'web' ? root : path.join(root, 'src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox'),
        env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', DOTNET_ENVIRONMENT: 'Development', ASPNETCORE_URLS: base },
        windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
    child.stdout.pipe(log, { end: false });
    child.stderr.pipe(log, { end: false });
    await until(async () => (await fetch(base + '/agents')).ok, 'host ready');
    browser = await chromium.launch({ headless: true });
    page = await browser.newPage({ viewport: result.viewport });
    page.setDefaultTimeout(30000);
    page.on('pageerror', error => result.errors.push(error.message));
    page.on('websocket', socket => {
        if (new URL(socket.url()).pathname.endsWith('/_blazor')) {
            socket.on('framereceived', () => circuitFrames++);
        }
    });
    if (feature === 'history') {
        await historyScenarios();
    } else if (feature === 'governance') {
        await governanceSmoke();
    } else {
        await diagnostics();
    }
    assert.equal(result.errors.length, 0);
})().catch(async error => {
    result.failure = error.message;
    result.failedState = await page?.getByTestId(feature === 'history' ? 'history-results-surface' : 'agents-' + feature + '-panel').innerText({ timeout: 1000 }).catch(() => 'Unavailable');
    process.exitCode = 1;
}).finally(async () => {
    await browser?.close();
    if (mode === 'web' && child?.exitCode === null) {
        await fetch(control + '/fixture/stop', { method: 'POST' }).catch(() => {});
        await pause(1000);
    }
    if (child?.exitCode === null) {
        if (process.platform === 'win32') {
            execFileSync('taskkill', ['/PID', String(child.pid), '/T', '/F'], { windowsHide: true, stdio: 'ignore' });
        } else {
            child.kill('SIGTERM');
        }
    }
    fs.writeFileSync(path.join(raw, feature + '-browser-' + mode + '.json'), JSON.stringify(result, null, 2));
    console.log(JSON.stringify(result));
});
