const { chromium } = require(process.env.OV_PLAYWRIGHT);
const fs = require('node:fs');
const path = require('node:path');
const { spawn, execFileSync } = require('node:child_process');
const assert = require('node:assert/strict');
const root = process.cwd();
const project = path.join(root, 'src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox');
const output = path.join(root, '.mcp-state/overview03/browser-sandbox-r2');
const scenarios = ['baseline','loading','initial-failure','stale-overview','empty','ready','header-partial','bound-unavailable',
    'usage-loading','stale-usage','scope-pending','wrong-scope','partial','unknown-unpriced','long-content','detail-pending'];
const noCharts = new Set(['loading','empty','usage-loading','scope-pending','wrong-scope']);
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
async function run(mode, port) {
    const out = path.join(output, mode);
    fs.mkdirSync(out, { recursive: true });
    const base = 'http://127.0.0.1:' + port;
    const stream = fs.createWriteStream(path.join(out, 'host.txt'));
    const child = spawn('dotnet', [path.join(project, 'bin', mode, 'Release/net10.0/CanDoItAll.AgentFramework.UiSandbox.dll'), '--urls', base],
        { cwd: project, env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', DOTNET_ENVIRONMENT: 'Development', CatalogAssetMode: mode },
          windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
    child.stdout.pipe(stream, { end: false });
    child.stderr.pipe(stream, { end: false });
    const result = { mode, ownedPid: child.pid, startedUtc: new Date().toISOString(), viewport: { width: 1600, height: 1000 }, scenarios: [], errors: [], requests: [], screenshots: [] };
    let browser;
    let page;
    async function until(test, message, timeout = 60000) {
        const end = Date.now() + timeout;
        while (Date.now() < end) {
            if (await test().catch(() => false)) return;
            assert(child.exitCode === null, 'Owned sandbox exited early');
            await pause(100);
        }
        throw new Error('Timeout: ' + message);
    }
    async function save(name) {
        await page.screenshot({ path: path.join(out, name + '.jpeg'), type: 'jpeg', quality: 82 });
        fs.writeFileSync(path.join(out, name + '-dom.txt'), await page.locator('body').innerText());
        result.screenshots.push(name);
    }
    async function geometry() {
        return page.getByTestId('agents-overview-dashboard').evaluate(e => {
            const box = node => { const r = node.getBoundingClientRect(); return { x: r.x, y: r.y, width: r.width, height: r.height, scrollWidth: node.scrollWidth }; };
            return { dashboard: box(e), grid: getComputedStyle(e.querySelector('.agents-overview-main-grid')).gridTemplateColumns,
                cards: [...e.querySelectorAll('.agents-overview-main-card')].map(box),
                charts: [...e.querySelectorAll('svg.apexcharts-svg')].map(svg => ({ ...box(svg), paths: [...svg.querySelectorAll('.apexcharts-series path[d]')].map(p => p.getAttribute('d')) })),
                fonts: document.fonts.status, images: [...e.querySelectorAll('img')].map(img => ({ loaded: img.complete && img.naturalWidth > 0, source: new URL(img.src).pathname })),
                scopeDirection: getComputedStyle(e.querySelector('[data-testid="agents-overview-usage-scope"] > div')).flexDirection,
                width: innerWidth };
        });
    }
    async function settled(expected) {
        let previous;
        let stableSince = 0;
        await until(async () => {
            const state = await geometry();
            const current = JSON.stringify(state.charts);
            if (state.charts.length !== expected || state.charts.some(chart => chart.width < 100 || chart.height < 100 || chart.paths.length === 0)) return false;
            if (current !== previous) { previous = current; stableSince = Date.now(); return false; }
            return Date.now() - stableSince > 400;
        }, 'actual chart settlement');
        await page.evaluate(() => document.fonts.ready);
        return geometry();
    }
    try {
        await until(async () => (await fetch(base + '/_dev/runtime')).ok, 'sandbox readiness');
        result.runtime = await fetch(base + '/_dev/runtime').then(r => r.json());
        assert.equal(result.runtime.assetMode, mode);
        browser = await chromium.launch({ headless: true });
        result.browser = browser.version();
        page = await browser.newPage({ viewport: result.viewport });
        page.on('pageerror', e => result.errors.push(e.message));
        page.on('request', r => result.requests.push({ path: new URL(r.url()).pathname, origin: new URL(r.url()).origin, type: r.resourceType() }));
        await page.goto(base + '/agents?specimen=overview&scenario=baseline&layout=matched&usageScope=both', { waitUntil: 'networkidle' });
        await page.getByTestId('agents-overview-dashboard').waitFor();
        const historyBefore = await page.evaluate(() => history.length);
        for (const scenario of scenarios) {
            if (scenario !== 'baseline') {
                await page.getByTestId('sandbox-overview-scenario').selectOption(scenario);
                await page.waitForURL(url => url.searchParams.get('scenario') === scenario);
            }
            const state = await settled(noCharts.has(scenario) ? 0 : 2);
            assert(state.dashboard.scrollWidth <= Math.ceil(state.dashboard.width) + 2, 'No horizontal dashboard overflow: ' + scenario);
            assert(state.images.every(image => image.loaded), 'Real avatars loaded: ' + scenario);
            const disabled = await page.getByTestId('agents-overview-open-model-usage').isDisabled();
            assert.equal(disabled, ['loading','usage-loading','scope-pending','wrong-scope','detail-pending'].includes(scenario));
            if (scenario === 'baseline') {
                assert.equal(state.cards.length, 4);
                assert(state.cards.every(card => Math.abs(card.height - 440) < 1));
                assert.equal(Math.round(state.dashboard.width), 1419);
                assert.equal(state.scopeDirection, 'row');
                result.baseline = state;
            }
            const text = await page.getByTestId('sandbox-overview-specimen').innerText();
            assert(!text.includes('Controlled private'));
            result.scenarios.push({ scenario, geometry: state, detailDisabled: disabled });
            await save(scenario);
        }
        assert.equal(await page.evaluate(() => history.length), historyBefore, 'Scenario changes replace history');
        await page.getByTestId('sandbox-overview-scenario').selectOption('ready');
        await page.waitForURL(url => url.searchParams.get('scenario') === 'ready');
        for (const [label, token] of [['Agents','agents'], ['Chats','chats'], ['Both','both']]) {
            await page.getByTestId('agents-overview-usage-scope').getByRole('button', { name: label, exact: true }).click();
            await page.waitForURL(url => url.searchParams.get('usageScope') === token);
            await settled(2);
            assert((await page.getByTestId('sandbox-intent').innerText()).includes('No query runs'));
            await save('scope-' + token);
        }
        assert.equal(await page.evaluate(() => history.length), historyBefore, 'Scope changes replace history');
        await page.getByTestId('agents-overview-open-provider-usage').click();
        await until(async () => (await page.getByTestId('sandbox-intent').innerText()).includes('No data-loading dialog opens'), 'sample detail intent acknowledged');
        assert(await page.getByTestId('agents-overview-open-provider-usage').isDisabled());
        await page.getByTestId('agents-overview-team-shortcut').first().click();
        await until(async () => (await page.getByTestId('sandbox-intent').innerText()).includes('No production navigation'), 'sample team intent acknowledged');
        assert.equal(new URL(page.url()).searchParams.get('specimen'), 'overview');
        await save('controlled-intents');
        await page.getByTestId('sandbox-overview-scenario').selectOption('wrong-scope');
        await page.getByTestId('agents-overview-usage-error').waitFor();
        await page.getByTestId('agents-overview-usage-retry').click();
        await page.getByTestId('agents-overview-usage-error').waitFor({ state: 'hidden' });
        await settled(2);
        await save('retry');
        await page.getByTestId('sandbox-overview-scenario').selectOption('long-content');
        await page.waitForURL(url => url.searchParams.get('scenario') === 'long-content');
        for (const width of [1280, 900]) {
            await page.setViewportSize({ width, height: 1000 });
            const state = await settled(2);
            assert(state.dashboard.scrollWidth <= Math.ceil(state.dashboard.width) + 2, 'Responsive dashboard overflow');
            await page.getByTestId('agents-overview-provider-bar').scrollIntoViewIfNeeded();
            await save('long-responsive-' + width);
            result.scenarios.push({ scenario: 'long-responsive-' + width, geometry: state });
        }
        await page.setViewportSize(result.viewport);
        await page.getByTestId('sandbox-catalog').click();
        await page.getByTestId('sandbox-normal').waitFor();
        await save('catalog-compatibility');
        await page.goto(base + '/agents?specimen=capabilities&scenario=baseline&layout=matched');
        await page.getByTestId('agents-capabilities-panel').waitFor();
        await save('capabilities-compatibility');
        assert(result.requests.some(request => /apex/i.test(request.path) && request.type === 'script'), 'Actual Apex chart script loaded');
        assert(result.requests.every(request => request.origin === base || request.origin === 'null'), 'No external source/runtime requests');
        assert.deepEqual(result.errors, []);
        result.status = 'PASS';
    } catch (error) {
        result.status = 'FAIL'; result.failure = error.stack; process.exitCode = 1;
        if (page) await save('failure').catch(() => {});
    } finally {
        if (browser) await browser.close();
        if (child.pid && child.exitCode === null) execFileSync('taskkill', ['/PID', String(child.pid), '/T', '/F'], { windowsHide: true, stdio: 'ignore' });
        await pause(250);
        result.hostStopped = child.exitCode !== null;
        result.finishedUtc = new Date().toISOString();
        fs.writeFileSync(path.join(out, 'summary.json'), JSON.stringify(result, null, 2));
        stream.end();
        console.log(JSON.stringify({ mode, status: result.status, scenarios: result.scenarios.length, screenshots: result.screenshots.length, failure: result.failure }));
    }
}
(async () => { await run('Parity', 5393); await run('Fast', 5394); })();
