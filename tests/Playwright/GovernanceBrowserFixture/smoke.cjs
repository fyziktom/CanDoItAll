const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { spawn, execFileSync } = require('node:child_process');
const crypto = require('node:crypto');
const { chromium } = require(process.env.GOV_PLAYWRIGHT);
const root = path.resolve(process.argv[2]);
const host = process.argv[3];
assert(['pre', 'post', 'sandbox'].includes(host));
const raw = path.join(root, '.artifacts/governance-final');
const plan = JSON.parse(fs.readFileSync(path.join(raw, 'smoke-plan.json'), 'utf8'));
assert.equal(execFileSync('dotnet', ['--version']).toString().trim(), plan.sdk);
const full = host !== 'sandbox';
const base = 'http://127.0.0.1:' + (full ? 5295 : 5395);
const url = base + (full ? '/agents?tab=governance&agentId=' + plan.agentId : '/agents?specimen=governance&scenario=selected-agent&layout=matched');
const files = host === 'pre' ? plan.preFiles : plan.postFiles;
const hashes = Object.fromEntries(Object.entries(files).map(([kind, file]) => [kind, hash(fs.readFileSync(path.join(root, file)))]));
if (host === 'pre') {
    assert.deepEqual(hashes, plan.preHashes);
}
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
const summary = { host, sdk: plan.sdk, viewport: plan.viewport, sourceFiles: files, sourceHashes: hashes,
    classification: 'process-cold-with-existing-restore-and-filesystem-cache', assetMode: full ? 'Production' : 'Parity',
    cold: [], warm: [], failures: [] };
let children = [];
let browser;
let page;
let edit;
let events = [];
let native = [];
let confirmations = 0;
function hash(bytes) {
    return crypto.createHash('sha256').update(bytes).digest('hex');
}
function writeSummary() {
    fs.writeFileSync(path.join(raw, 'smoke-' + host + '.json'), JSON.stringify(summary, null, 2));
}
function start(label, executable, args, env, repetition) {
    const log = fs.createWriteStream(path.join(raw, `smoke-${host}-${repetition}-${label}.log`));
    const child = spawn(executable, args, { cwd: root, env, windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
    children.push(child);
    for (const input of [child.stdout, child.stderr]) {
        input.on('data', chunk => {
            log.write(chunk);
            native.push(chunk.toString());
        });
    }
    child.on('exit', () => log.end());
    return child;
}
async function stop() {
    if (browser) {
        await browser.close();
        browser = null;
    }
    for (const child of children.reverse()) {
        if (child.exitCode === null) {
            if (process.platform === 'win32') {
                execFileSync('taskkill', ['/PID', String(child.pid), '/T', '/F'], { windowsHide: true, stdio: 'ignore' });
            } else {
                child.kill('SIGTERM');
            }
        }
    }
    children = [];
}
async function until(predicate, description, timeout) {
    const deadline = Date.now() + timeout;
    let last;
    while (Date.now() < deadline) {
        try {
            if (await predicate()) {
                return;
            }
        } catch (error) {
            last = error;
        }
        if (children.some(child => child.exitCode !== null)) {
            throw Error('An owned host exited during ' + description);
        }
        await pause(150);
    }
    throw Error('Timed out: ' + description + (last ? ' (' + last.name + ')' : ''));
}
async function probe() {
    const response = await fetch(base + '/_dev/runtime', { signal: AbortSignal.timeout(4000) });
    assert(response.ok);
    const data = await response.json();
    return { isReady: data.isReady, runtimePid: data.runtimePid, watchIteration: data.watchIteration,
        hotReloadGeneration: data.hotReloadGeneration, ownerId: data.ownerId };
}
async function confirmProfile() {
    if (!full) {
        return;
    }
    const button = page.getByRole('button', { name: 'Continue', exact: true });
    if (await button.isVisible().catch(() => false)) {
        await button.click({ timeout: 2000 });
        confirmations++;
    }
}
async function contentReady() {
    await confirmProfile();
    return await page.getByTestId('agents-governance-detail-title').innerText().then(text => text === 'Governance run 1').catch(() => false);
}
async function documentIdentity() {
    return page.evaluate(() => window.governanceSmokeDocument).catch(() => null);
}
async function predicate(kind, changed) {
    await confirmProfile();
    if (kind === 'css') {
        return page.getByTestId('agents-governance-detail-title').evaluate(element => getComputedStyle(element).outlineStyle)
            .then(value => changed ? value === 'dashed' : value !== 'dashed').catch(() => false);
    }
    const token = kind === 'razor' ? 'Governance Razor smoke.' : 'G-C-SMOKE';
    return page.getByTestId('agents-governance-panel').innerText().then(text => text.includes(token) === changed).catch(() => false);
}
async function sample(kind, repetition) {
    const file = path.resolve(root, files[kind]);
    assert(file.startsWith(root + path.sep));
    const original = fs.readFileSync(file);
    assert.equal(hash(original), hashes[kind]);
    const text = original.toString('utf8');
    let changed;
    if (kind === 'razor') {
        const before = 'Inspect durable execution state, approvals, artifacts, checkpoints, and receipts.';
        assert.equal(text.split(before).length, 2);
        changed = Buffer.from(text.replace(before, before + ' Governance Razor smoke.'));
    } else if (kind === 'csharp') {
        const before = 'value.ToString(CultureInfo.InvariantCulture);';
        assert.equal(text.split(before).length, 2);
        changed = Buffer.from(text.replace(before, 'value.ToString(CultureInfo.InvariantCulture) + " G-C-SMOKE";'));
    } else {
        changed = Buffer.from(text + '\n.governance-surface ::deep [data-testid="agents-governance-detail-title"] { outline: 2px dashed rgb(190, 18, 60); outline-offset: 2px; }\n');
    }
    const before = await probe();
    const documentBefore = await documentIdentity();
    const cursor = events.length;
    const logCursor = native.length;
    const beforeConfirmations = confirmations;
    const started = performance.now();
    edit = { file, original, changed };
    fs.writeFileSync(file, changed);
    const result = { kind, repetition, before };
    try {
        await until(() => predicate(kind, true), kind + ' visible edit', plan.editTimeoutMs);
        const visible = performance.now();
        await pause(750);
        await until(() => predicate(kind, true), kind + ' settled edit', 15000);
        const after = await probe();
        const documentAfter = await documentIdentity();
        const seen = events.slice(cursor);
        const applied = /changes applied|hot reload|Hot reload/i.test(native.slice(logCursor).join('')) || seen.some(event => event === 'ApplyManagedCodeUpdates' || event === 'UpdateStaticFile');
        result.classification = after.runtimePid !== before.runtimePid ? 'restart'
            : documentAfter !== documentBefore || seen.includes('navigation') ? 'browser-reload'
            : applied ? 'hot-reload' : 'failure';
        result.editToVisibleMs = Math.round(visible - started);
        result.after = after;
        result.success = result.classification !== 'failure';
    } catch (error) {
        result.classification = 'failure';
        result.success = false;
        result.elapsedMs = Math.round(performance.now() - started);
        result.reason = error.message;
    } finally {
        assert(fs.readFileSync(file).equals(changed), 'Concurrent source edit; refusing to overwrite it.');
        fs.writeFileSync(file, original);
        edit = null;
        await until(() => predicate(kind, false), kind + ' restored baseline', plan.editTimeoutMs);
    }
    result.profileConfirmations = confirmations - beforeConfirmations;
    summary.warm.push(result);
    writeSummary();
    console.log(JSON.stringify({ host, ...result }));
}
(async () => {
    for (let repetition = 1; repetition <= plan.coldStarts; repetition++) {
        const env = { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', DOTNET_ENVIRONMENT: 'Development', ASPNETCORE_URLS: base,
            DOTNET_CLI_UI_LANGUAGE: 'en', DOTNET_WATCH_SUPPRESS_EMOJIS: '1', DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER: '1',
            DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH: '0', CanDoItAllMcpOwnerKind: 'DirectGovernanceSmoke',
            CanDoItAllMcpOwnerId: 'governance-' + host + '-' + repetition, Logging__LogLevel__Default: 'Warning' };
        if (full) {
            Object.assign(env, JSON.parse(fs.readFileSync(path.join(raw, 'browser-data/environment.json'), 'utf8')));
        } else {
            env.CatalogAssetMode = 'Parity';
        }
        native = [];
        events = [];
        const started = performance.now();
        start('tailwind', process.execPath, [path.join(root, 'Tailwind/node_modules/@tailwindcss/cli/dist/index.mjs'),
            '-i', path.join(root, 'Tailwind/input.css'), '-o', path.join(root, 'src/App/CanDoItAll.Web/wwwroot/css/output.css'), '--watch=always'], env, repetition);
        start('watch', 'dotnet', ['watch', '--verbose', '--non-interactive', '--project',
            full ? 'src/App/CanDoItAll.Web' : 'src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox',
            '--no-launch-profile', '--property:CatalogAssetMode=Parity'], env, repetition);
        await until(async () => (await probe()).isReady, 'runtime ready', plan.readyTimeoutMs);
        browser = await chromium.launch({ headless: true });
        summary.browser = browser.version();
        summary.node = process.version;
        page = await browser.newPage({ viewport: plan.viewport });
        await page.addInitScript(() => window.governanceSmokeDocument = crypto.randomUUID());
        page.on('framenavigated', frame => {
            if (frame === page.mainFrame()) {
                events.push('navigation');
            }
        });
        page.on('websocket', socket => socket.on('framereceived', frame => {
            try {
                const item = JSON.parse(String(frame.payload));
                if (['ApplyManagedCodeUpdates', 'UpdateStaticFile', 'Reload', 'RefreshBrowser'].includes(item.type)) {
                    events.push(item.type);
                }
            } catch {}
        }));
        await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 90000 });
        await until(contentReady, 'interactive Governance fixture', 90000);
        summary.cold.push({ repetition, visibleMs: Math.round(performance.now() - started), runtime: await probe(), profileConfirmations: confirmations });
        writeSummary();
        console.log(JSON.stringify({ host, cold: summary.cold.at(-1) }));
        if (repetition === plan.coldStarts) {
            for (const kind of ['razor', 'csharp', 'css']) {
                for (let attempt = 1; attempt <= plan.warmRepetitions; attempt++) {
                    await sample(kind, attempt);
                }
            }
        }
        await stop();
        await pause(750);
    }
})().catch(error => {
    summary.failures.push(error.message);
    process.exitCode = 1;
}).finally(async () => {
    if (edit && fs.readFileSync(edit.file).equals(edit.changed)) {
        fs.writeFileSync(edit.file, edit.original);
    }
    await stop().catch(error => summary.failures.push(error.message));
    writeSummary();
    if (summary.warm.some(row => !row.success) || summary.failures.length) {
        process.exitCode = 1;
    }
    console.log(JSON.stringify({ host, coldStarts: summary.cold.length, warmEdits: summary.warm.length, failures: summary.failures }));
});
