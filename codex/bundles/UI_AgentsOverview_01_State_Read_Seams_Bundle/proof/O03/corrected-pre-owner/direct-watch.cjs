const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const { spawn, execFileSync } = require('node:child_process');
const args = Object.fromEntries(process.argv.slice(2).reduce((items, value, index, values) => {
    if (value.startsWith('--')) {
        items.push([value.slice(2), values[index + 1]]);
    }
    return items;
}, []));
const root = path.resolve(args.repo || process.cwd());
const planPath = path.resolve(args.plan);
const plan = JSON.parse(fs.readFileSync(planPath, 'utf8'));
const host = args.host;
const phase = args.phase;
assert(plan.hosts.includes(host));
assert(['calibrate', 'cold', 'warm'].includes(phase));
const closure = path.join(root, 'codex/bundles/UI_AgentsOverview_01_State_Read_Seams_Bundle/proof/O02/closure.md');
assert(fs.existsSync(closure) && /CLOSED/.test(fs.readFileSync(closure, 'utf8')), 'O02 closure is required before this tool runs.');
const fullapp = host.endsWith('fullapp');
const mode = host === 'fast' ? 'Fast' : 'Parity';
const port = fullapp ? 5293 : host === 'fast' ? 5394 : 5393;
const base = 'http://127.0.0.1:' + port;
const url = base + (fullapp ? '/agents?tab=overview&usageScope=both' : '/agents?specimen=overview&scenario=baseline&layout=matched&usageScope=both');
const runId = host + '-' + phase + '-' + new Date().toISOString().replace(/[:.]/g, '-');
const out = path.resolve(args.output, runId);
fs.mkdirSync(out, { recursive: true });
const clock = () => process.hrtime.bigint();
const sha = data => crypto.createHash('sha256').update(data).digest('hex');
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
const log = value => {
    const row = { utc: new Date().toISOString(), ...value };
    fs.appendFileSync(path.join(out, 'ledger.jsonl'), JSON.stringify(row) + '\n');
    process.stdout.write(JSON.stringify(row) + '\n');
};
const children = [];
const events = [];
const browserEvents = [];
const samples = [];
let browser;
let page;
let activeEdit;
let confirmations = 0;
let filterRestorations = 0;
const productionCss = path.join(root, 'src/App/CanDoItAll.Web/wwwroot/css/output.css');
const productionBefore = fs.readFileSync(productionCss);
function start(label, command, argv, env) {
    const stream = fs.createWriteStream(path.join(out, label + '.txt'));
    stream.write(JSON.stringify({ cwd: root, command, argv }) + '\n');
    const child = spawn(command, argv, { cwd: root, env, windowsHide: true, detached: process.platform !== 'win32', stdio: ['pipe', 'pipe', 'pipe'] });
    children.push(child);
    for (const input of [child.stdout, child.stderr]) {
        let pending = '';
        input.on('data', data => {
            stream.write(data);
            pending += data.toString();
            const lines = pending.split(/\r?\n/);
            pending = lines.pop();
            for (const line of lines) {
                events.push({ label, ns: clock().toString(), text: line.replace(/\x1b\[[0-9;]*m/g, '') });
            }
        });
    }
    child.on('close', code => {
        stream.end();
        log({ kind: 'owned-process-exit', label, pid: child.pid, code });
    });
    log({ kind: 'owned-process-start', label, pid: child.pid, command, argv });
    return child;
}
function stop(child) {
    if (!child.pid || child.exitCode !== null) {
        return;
    }
    if (process.platform === 'win32') {
        execFileSync('taskkill', ['/PID', String(child.pid), '/T', '/F'], { windowsHide: true, stdio: 'ignore' });
    } else {
        process.kill(-child.pid, 'SIGTERM');
    }
}
function flush(file, bytes) {
    assert(file.startsWith(root + path.sep), 'Only an exact owned workspace probe is writable.');
    const fd = fs.openSync(file, 'w');
    try {
        fs.writeFileSync(fd, bytes);
        fs.fsyncSync(fd);
    } finally {
        fs.closeSync(fd);
    }
}
async function probe() {
    const response = await fetch(base + '/_dev/runtime', { signal: AbortSignal.timeout(4000) });
    assert(response.ok);
    const value = await response.json();
    return { isReady: value.isReady, runtimePid: value.runtimePid, watchIteration: value.watchIteration,
        hotReloadGeneration: value.hotReloadGeneration, assetMode: value.assetMode || 'Production',
        ownerKind: value.ownerKind, ownerId: value.ownerId };
}
async function waitReady() {
    const deadline = Date.now() + plan.readyTimeoutMs;
    while (Date.now() < deadline) {
        try {
            const state = await probe();
            if (state.isReady) {
                assert.equal(state.ownerKind, 'DirectOverviewBenchmark');
                assert.equal(state.ownerId, runId);
                assert(state.watchIteration >= 1);
                if (!fullapp) {
                    assert.equal(state.assetMode, mode);
                }
                return state;
            }
        } catch (error) {
            if (error.code === 'ERR_ASSERTION') {
                throw error;
            }
        }
        assert(!children.some(child => child.exitCode !== null), 'An owned host exited before readiness.');
        await pause(300);
    }
    throw new Error('Direct watch did not become ready within the frozen limit.');
}
async function documentState() {
    return page.evaluate(() => ({ id: window.overviewDocumentIdentity, timeOrigin: performance.timeOrigin })).catch(() => ({ id: null }));
}
async function instrumentBrowser() {
    await page.exposeFunction('overviewDiagnosticEvent', kind => browserEvents.push({ ns: clock().toString(), kind }));
    await page.addInitScript(() => {
        window.overviewDocumentIdentity = crypto.randomUUID();
        const timer = setInterval(() => {
            if (!window.Blazor?.addEventListener) {
                return;
            }
            clearInterval(timer);
            for (const name of ['enhancednavigationstart', 'enhancedload', 'enhancednavigationend']) {
                window.Blazor.addEventListener(name, () => window.overviewDiagnosticEvent(name));
            }
        }, 50);
    });
    page.on('websocket', socket => socket.on('framereceived', frame => {
        try {
            const value = JSON.parse(String(frame.payload));
            if (['RefreshBrowser', 'Reload', 'Wait', 'UpdateStaticFile', 'ApplyManagedCodeUpdates'].includes(value.type)) {
                browserEvents.push({ ns: clock().toString(), kind: 'sdk-message', type: value.type });
            }
        } catch {}
    }));
    page.on('framenavigated', frame => {
        if (frame === page.mainFrame()) {
            browserEvents.push({ ns: clock().toString(), kind: 'main-frame-navigation' });
        }
    });
    page.on('pageerror', error => browserEvents.push({ ns: clock().toString(), kind: 'page-error', message: error.message }));
}
async function restoreContext() {
    if (fullapp) {
        const proceed = page.getByRole('button', { name: 'Continue', exact: true });
        if (await proceed.isVisible().catch(() => false)) {
            await proceed.click({ timeout: 2000 });
            confirmations++;
        }
    }
 }
async function chartFrame() {
    return page.evaluate(() => {
        const root = document.querySelector('[data-testid="agents-overview-dashboard"]');
        if (!root || document.querySelector('[data-testid="agents-overview-load-error"], [data-testid="agents-overview-usage-error"]')) {
            return null;
        }
        const charts = [...root.querySelectorAll('.apexcharts-canvas svg.apexcharts-svg')];
        if (charts.length !== 2) {
            return null;
        }
        const result = [];
        for (const svg of charts) {
            const r = svg.getBoundingClientRect();
            const paths = [...svg.querySelectorAll('.apexcharts-series path[d]')].map(p => p.getAttribute('d')).filter(d => d.length > 12);
            if (r.width < 100 || r.height < 100 || r.top < 0 || r.bottom > innerHeight || r.left < 0 || r.right > innerWidth || paths.length < 2) {
                return null;
            }
            result.push({ width: r.width, height: r.height, x: r.x, y: r.y, paths });
        }
        const metric = document.querySelector('[data-testid="agents-overview-metric-usage"] .agents-overview-stat-badge__value')?.textContent.trim();
        if (metric !== '6') {
            return null;
        }
        return result;
    }).catch(() => null);
}
async function settleCharts() {
    const deadline = Date.now() + 15000;
    let prior;
    let stableSince = 0;
    while (Date.now() < deadline) {
        const frame = await chartFrame();
        const signature = frame ? JSON.stringify(frame) : null;
        if (signature && signature === prior) {
            if (Date.now() - stableSince >= 350) {
                await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
                return frame;
            }
        } else {
            prior = signature;
            stableSince = Date.now();
        }
        await pause(50);
    }
    throw new Error('Two actual chart series and their frame did not settle.');
}
async function fixtureReady() {
    await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 90000 });
    const deadline = Date.now() + 90000;
    while (Date.now() < deadline) {
        await restoreContext();
        const startupAccepted = !fullapp || await page.evaluate(() => window.CanDoItAll?.browserState?.isDatabaseStartupPromptDismissed?.() === true).catch(() => false);
        if (startupAccepted && await chartFrame()) {
            await page.evaluate(() => document.fonts.ready);
            await settleCharts();
            return;
        }
        await pause(100);
    }
    throw new Error('The exact interactive Overview fixture did not become visible.');
}

async function predicate(edit, expected) {
    const deadline = Date.now() + plan.timeoutMs;
    while (Date.now() < deadline) {
        await restoreContext();
        const visible = await page.evaluate(({ edit, expected }) => {
            const found = document.querySelectorAll(edit.selector);
            if (found.length !== 1) {
                return false;
            }
            const e = found[0];
            if (!e) {
                return false;
            }
            const r = e.getBoundingClientRect();
            if (r.width <= 0 || r.height <= 0 || r.top < 0 || r.left < 0 || r.bottom > innerHeight || r.right > innerWidth) {
                return false;
            }
            for (let ancestor = e.parentElement; ancestor; ancestor = ancestor.parentElement) {
                const style = getComputedStyle(ancestor);
                if (['auto', 'scroll', 'hidden', 'clip'].includes(style.overflowY)) {
                    const a = ancestor.getBoundingClientRect();
                    if (r.top < a.top || r.bottom > a.bottom) {
                        return false;
                    }
                }
            }
            return edit.property ? Math.abs(parseFloat(getComputedStyle(e)[edit.property]) - expected) < 0.03 : e.textContent.trim() === expected;
        }, { edit, expected }).catch(() => false);
        if (visible) {
            await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
            return;
        }
        await pause(50);
    }
    throw new Error('Browser-visible predicate timed out: ' + edit.id);
}
async function settle(cursor, prior, edit, expected) {
    const start = Date.now();
    let lastCount = browserEvents.length;
    let lastChange = start;
    while (Date.now() - start < 15000) {
        await restoreContext();
        if (lastCount !== browserEvents.length) {
            lastCount = browserEvents.length;
            lastChange = Date.now();
        }
        const state = await documentState();
        const observed = browserEvents.slice(cursor);
        const starts = observed.filter(e => e.kind === 'enhancednavigationstart').length;
        const ends = observed.filter(e => e.kind === 'enhancednavigationend').length;
        if (state.id && (starts === 0 || ends >= starts || state.id !== prior.id)
            && Date.now() - start >= plan.settlementMinimumMs && Date.now() - lastChange >= plan.settlementQuietMs) {
            await predicate(edit, expected);
            const charts = await settleCharts();
            return { starts, ends, waitMs: Date.now() - start, charts };
        }
        await pause(100);
    }
    throw new Error('Browser navigation did not settle.');
}
async function observe(edit, repetition, direction, bytes, file) {
    const before = await probe();
    const documentBefore = await documentState();
    const cursor = events.length;
    const browserCursor = browserEvents.length;
    const priorConfirmations = confirmations;
    const priorFilters = filterRestorations;
    flush(file, bytes);
    const t0 = clock();
    const row = { host, phase, editId: edit.id, category: edit.category, repetition, direction,
        flushedSha256: sha(bytes), flushNs: t0.toString(), utc: new Date().toISOString(), before, documentBefore };
    try {
        const expected = direction === 'forward' ? edit.after : edit.before;
        await predicate(edit, expected);
        const firstVisible = clock();
        const settlement = await settle(browserCursor, documentBefore, edit, expected);
        const finalVisible = clock();
        const after = await probe();
        const documentAfter = await documentState();
        const seen = browserEvents.slice(browserCursor);
        const native = events.slice(cursor).filter(e => /changes applied|Hot reload|Restart|rude edit|error/i.test(e.text));
        const classification = after.runtimePid !== before.runtimePid ? 'restart'
            : documentAfter.id !== documentBefore.id || seen.some(e => e.kind === 'main-frame-navigation') ? 'browser-reload'
            : seen.some(e => e.kind === 'enhancednavigationstart') ? 'browser-reload-enhanced'
            : native.some(e => /changes applied/.test(e.text)) || seen.some(e => e.kind === 'sdk-message') ? 'hot-reload' : 'inconclusive';
        Object.assign(row, { success: classification !== 'inconclusive', classification, firstVisibleNs: firstVisible.toString(),
            firstVisibleMs: Number(firstVisible - t0) / 1e6, settledVisibleMs: Number(finalVisible - t0) / 1e6,
            settlement, after, documentAfter, native, browserEvents: seen });
    } catch (error) {
        const native = events.slice(cursor).filter(e => /changes applied|Hot reload|Restart|rude edit|error|not supported/i.test(e.text));
        Object.assign(row, { success: false, classification: native.some(e => /rude edit/i.test(e.text)) ? 'rude-edit'
            : native.some(e => /not supported/i.test(e.text)) ? 'unsupported-edit' : 'failed-observation',
            elapsedMs: Number(clock() - t0) / 1e6, error: error.message, native, browserEvents: browserEvents.slice(browserCursor) });
    }
    row.databaseConfirmations = confirmations - priorConfirmations;
    row.filterRestorations = filterRestorations - priorFilters;
    samples.push(row);
    log({ kind: 'sample', ...row });
    return row;
}
async function trial(edit, repetition) {
    const relative = host === 'pre-fullapp' ? edit.prePath : edit.postPath;
    const file = path.resolve(root, relative);
    const original = fs.readFileSync(file);
    const expectedHash = host === 'pre-fullapp' ? edit.preSha256 : plan.postHashes[relative];
    assert.equal(sha(original), expectedHash);
    const text = original.toString('utf8');
    assert.equal(text.split(edit.old).length, 2);
    const changed = Buffer.from(text.replace(edit.old, edit.replacement));
    await predicate(edit, edit.before);
    activeEdit = { file, original, changed };
    let forward;
    let reverse;
    try {
        forward = await observe(edit, repetition, 'forward', changed, file);
    } finally {
        assert.deepEqual(fs.readFileSync(file), changed, 'Concurrent source edit; refusing to overwrite.');
        reverse = await observe(edit, repetition, 'reverse', original, file);
        assert.deepEqual(fs.readFileSync(file), original);
        activeEdit = null;
    }
    assert(forward.success && reverse.success, 'Observation failed; retain both directions and review before retrying.');
}
(async () => {
    assert.equal(sha(fs.readFileSync(__filename)), plan.harnessSha256, 'The harness is not the frozen version.');
    assert.equal(execFileSync('dotnet', ['--version']).toString().trim(), plan.sdk);
    const env = { ...process.env };
    if (fullapp) {
        const fixturePath = path.resolve(args.environment);
        assert(fixturePath.startsWith(path.join(root, '.mcp-state') + path.sep));
        const fixture = JSON.parse(fs.readFileSync(fixturePath, 'utf8'));
        const database = /(?:^|;)Database=([^;]+)/i.exec(fixture.Database__ConnectionString)?.[1];
        assert.equal(database, plan.fixture.databaseName, 'Only the exact isolated fixture database is allowed.');
        assert(!/(?:Password|Pwd)=/i.test(fixture.Database__ConnectionString));
        Object.assign(env, fixture);
        env.DevelopmentManager__TuningModeEnabled = 'false';
    }
    Object.assign(env, { ASPNETCORE_ENVIRONMENT: 'Development', DOTNET_ENVIRONMENT: 'Development', ASPNETCORE_URLS: base,
        DOTNET_CLI_UI_LANGUAGE: 'en', DOTNET_WATCH_SUPPRESS_EMOJIS: '1', DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER: '1',
        DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH: '0', Logging__LogLevel__Default: 'Warning',
        CanDoItAllMcpOwnerKind: 'DirectOverviewBenchmark', CanDoItAllMcpOwnerId: runId });
    if (!fullapp) {
        env.CatalogAssetMode = mode;
    }
    const started = clock();
    const cssInput = host === 'fast' ? 'Tailwind/catalog-fast.css' : 'Tailwind/input.css';
    const cssOutput = host === 'fast' ? 'src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/wwwroot/css/catalog-fast.css' : 'src/App/CanDoItAll.Web/wwwroot/css/output.css';
    start('tailwind', process.execPath, [path.join(root, 'Tailwind/node_modules/@tailwindcss/cli/dist/index.mjs'), '-i', path.join(root, cssInput), '-o', path.join(root, cssOutput), '--watch=always'], env);
    const project = fullapp ? 'src/App/CanDoItAll.Web' : 'src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox';
    start('dotnet-watch', 'dotnet', ['watch', '--verbose', '--non-interactive', '--project', project, '--no-launch-profile', '--property:CatalogAssetMode=' + mode], env);
    const ready = await waitReady();
    const { chromium } = require(process.env.OVERVIEW_PLAYWRIGHT);
    browser = await chromium.launch({ headless: true });
    page = await browser.newPage({ viewport: plan.viewport });
    await instrumentBrowser();
    await fixtureReady();
    await page.mouse.move(1590, 990);
    for (const edit of plan.edits) {
        await predicate(edit, edit.before);
    }
    await settle(browserEvents.length, await documentState(), plan.edits[0], plan.edits[0].before);
    const coldVisible = clock();
    log({ kind: 'interactive-ready', host, phase, repetition: Number(args.repetition || 1), elapsedMs: Number(coldVisible - started) / 1e6,
        confirmations, filterRestorations, ready, planSha256: sha(fs.readFileSync(planPath)), browser: browser.version(), node: process.version,
        productionCssSha256: sha(fs.readFileSync(productionCss)), charts: await chartFrame(), classification: 'process-cold-with-existing-restore-and-filesystem-cache' });
    for (const edit of plan.edits) {
        await predicate(edit, edit.before);
    }
    fs.writeFileSync(path.join(out, 'ready-dom.html'), await page.content());
    await page.screenshot({ path: path.join(out, 'ready.jpeg'), type: 'jpeg', quality: 80 });
    if (phase === 'warm') {
        for (const edit of plan.edits) {
            for (let repetition = 1; repetition <= plan.repetitions; repetition++) {
                await trial(edit, repetition);
                await pause(750);
            }
        }
    }
    log({ kind: 'complete', host, phase, samples: samples.length });
})().catch(async error => {
    log({ kind: 'failure', error: error.message });
    if (page) {
        await page.screenshot({ path: path.join(out, 'failure.jpeg'), type: 'jpeg', quality: 80 }).catch(() => {});
        fs.writeFileSync(path.join(out, 'failed-dom.txt'), await page.locator('body').innerText().catch(() => 'Unavailable'));
        fs.writeFileSync(path.join(out, 'failed-dom.html'), await page.content().catch(() => 'Unavailable'));
        const probes = await page.evaluate(edits => edits.map(e => ({ id: e.id, selector: e.selector, matches: [...document.querySelectorAll(e.selector)].map(n => ({ text: n.textContent.trim(), fontSize: getComputedStyle(n).fontSize, rect: n.getBoundingClientRect().toJSON() })) })), plan.edits).catch(() => []);
        fs.writeFileSync(path.join(out, 'failed-probes.json'), JSON.stringify(probes, null, 2));
    }
    process.exitCode = 1;
}).finally(async () => {
    try {
        if (activeEdit && fs.readFileSync(activeEdit.file).equals(activeEdit.changed)) {
            flush(activeEdit.file, activeEdit.original);
            log({ kind: 'emergency-owned-restoration', file: path.relative(root, activeEdit.file), sha256: sha(activeEdit.original) });
        }
    } catch (error) {
        log({ kind: 'restoration-requires-owner-review', file: path.relative(root, activeEdit.file), error: error.message });
        process.exitCode = 1;
    }
    fs.writeFileSync(path.join(out, 'samples.json'), JSON.stringify(samples, null, 2));
    fs.writeFileSync(path.join(out, 'watch-events.json'), JSON.stringify(events, null, 2));
    fs.writeFileSync(path.join(out, 'browser-events.json'), JSON.stringify(browserEvents, null, 2));
    if (browser) {
        await browser.close();
    }
    for (const child of [...children].reverse()) {
        try {
            stop(child);
        } catch (error) {
            log({ kind: 'stop-error', pid: child.pid, error: error.message });
            process.exitCode = 1;
        }
    }
    if (host === 'fast' && !fs.readFileSync(productionCss).equals(productionBefore)) {
        log({ kind: 'asset-isolation-failure' });
        process.exitCode = 1;
    }
});
