const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { spawn, execFileSync } = require('node:child_process');
const assert = require('node:assert/strict');
const args = Object.fromEntries(process.argv.slice(2).reduce((all, value, index, values) => {
  if (value.startsWith('--')) all.push([value.slice(2), values[index + 1]]);
  return all;
}, []));
const root = path.resolve(args.repo || process.cwd());
const bundle = path.resolve(__dirname, '..');
const plan = JSON.parse(fs.readFileSync(path.join(bundle, 'plan/frozen-direct-edits.json'), 'utf8'));
const host = args.host;
assert(plan.hosts.includes(host), 'Choose fullapp, parity or fast.');
const phase = args.phase || 'acceptance';
assert(['acceptance', 'warm'].includes(phase));
const modulePath = args['playwright-module'] || process.env.CATALOG_PLAYWRIGHT_MODULE || 'playwright';
const { chromium } = require(modulePath);
const runId = host + '-' + phase + '-' + new Date().toISOString().replace(/[:.]/g, '-');
const output = path.resolve(args.output || path.join(root, '.mcp-state/catalog-harden'), runId);
fs.mkdirSync(output, { recursive: true });
const ledgerFile = path.join(output, 'ledger.jsonl');
const log = value => {
  const row = { utc: new Date().toISOString(), ...value };
  fs.appendFileSync(ledgerFile, JSON.stringify(row) + '\n');
  process.stdout.write(JSON.stringify(row) + '\n');
};
const clock = () => process.hrtime.bigint();
const sha = value => crypto.createHash('sha256').update(value).digest('hex');
const productionCss = path.join(root, 'src/App/CanDoItAll.Web/wwwroot/css/output.css');
const productionBefore = fs.readFileSync(productionCss);
const children = [];
const events = [];
let browser;
let page;
let activeEdit;
let navigation = 0;
let confirmations = 0;
const mode = host === 'fast' ? 'Fast' : 'Parity';
const port = host === 'fullapp' ? 5291 : host === 'fast' ? 5392 : 5391;
const base = 'http://127.0.0.1:' + port;
const url = base + (host === 'fullapp' ? '/agents?tab=agents' : '/agents');
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
const safeError = error => String(error.message || error).replace(/(?:Password|Pwd)=([^;\s]+)/gi, 'Password=<redacted>').slice(0, 1800);
function start(label, command, argv, env) {
  const stream = fs.createWriteStream(path.join(output, label + '.txt'));
  stream.write(JSON.stringify({ utc: new Date().toISOString(), cwd: root, command, argv }) + '\n');
  const child = spawn(command, argv, { cwd: root, env, detached: process.platform !== 'win32', windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
  children.push(child);
  let pending = '';
  for (const input of [child.stdout, child.stderr]) input.on('data', data => {
    stream.write(data);
    pending += data.toString();
    const lines = pending.split(/\r?\n/);
    pending = lines.pop();
    for (const text of lines) events.push({ label, sequence: events.length, ns: clock().toString(), text });
  });
  child.on('error', error => log({ kind: 'process-error', label, error: safeError(error) }));
  child.on('close', (code, signal) => { stream.end(); log({ kind: 'process-exit', label, pid: child.pid, code, signal }); });
  log({ kind: 'process-start', label, pid: child.pid, command, argv });
  return child;
}
function stop(child) {
  if (!child.pid || child.exitCode !== null) return;
  try {
    if (process.platform === 'win32') execFileSync('taskkill', ['/PID', String(child.pid), '/T', '/F'], { windowsHide: true, stdio: 'ignore' });
    else process.kill(-child.pid, 'SIGTERM');
  } catch {}
}
function flush(file, bytes) {
  const fd = fs.openSync(file, 'w');
  try { fs.writeFileSync(fd, bytes); fs.fsyncSync(fd); } finally { fs.closeSync(fd); }
}
async function probe() {
  const response = await fetch(base + '/_dev/runtime', { signal: AbortSignal.timeout(4000) });
  assert(response.ok, 'Runtime probe failed.');
  const value = await response.json();
  return { isReady: value.isReady, runtimePid: value.runtimePid, watchIteration: value.watchIteration,
    hotReloadGeneration: value.hotReloadGeneration, assetMode: value.assetMode || 'Production',
    ownerKind: value.ownerKind, ownerId: value.ownerId };
}
async function waitReady() {
  const deadline = Date.now() + plan.readyTimeoutMs;
  let current;
  while (Date.now() < deadline) {
    try { current = await probe(); if (current.isReady) break; } catch {}
    if (children.some(c => c.exitCode !== null)) throw new Error('Owned process exited before readiness.');
    await pause(500);
  }
  assert(current?.isReady, 'Runtime readiness timeout.');
  assert.equal(current.ownerKind, 'DirectCatalogBenchmark');
  assert.equal(current.ownerId, runId);
  assert(current.watchIteration >= 1, 'Runtime is not a dotnet watch child.');
  if (host !== 'fullapp') assert.equal(current.assetMode, mode);
  return current;
}
async function fixture() {
  await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 90000 });
  const search = page.getByTestId('agents-catalog-search');
  const deadline = Date.now() + 120000;
  let filtered = false;
  let stable = 0;
  let attempts = 0;
  while (Date.now() < deadline && stable < 8) {
    attempts++;
    await confirmIsolatedDatabase();
    if (!await search.isVisible().catch(() => false)) {
      filtered = false;
      stable = 0;
      await pause(500);
      continue;
    }
    try {
      if (!filtered) {
        await search.fill('', { timeout: 2000 });
        await search.fill('catalog-fixture', { timeout: 2000 });
        await pause(500);
        filtered = await page.getByTestId('agents-catalog-card').count() === 12;
        if (filtered) log({ kind: 'interactive-readiness', attempts, filteredCards: 12 });
      } else {
        if (await search.inputValue({ timeout: 2000 }) !== '') await search.fill('', { timeout: 2000 });
        const visible = await page.getByTestId('agents-catalog-new').isVisible().catch(() => false)
          && await page.getByTestId('agents-catalog-card').count() === 40;
        stable = visible ? stable + 1 : 0;
      }
    } catch (error) {
      if (!/Timeout|closed|destroyed|navigation/i.test(error.message)) throw error;
      filtered = false;
      stable = 0;
    }
    await pause(500);
  }
  assert(stable === 8, 'Interactive fixture did not stabilize after public search 40 -> 12 -> 40.');
  await page.evaluate(async () => {
    window.scrollTo(0, 0);
    for (const node of document.querySelectorAll('.agent-catalog-panel__agent-scroll, .agent-catalog-panel__team-panel')) node.scrollTop = 0;
    await document.fonts.ready;
  });
}

async function confirmIsolatedDatabase() {
  if (host !== 'fullapp') return;
  const proceed = page.getByRole('button', { name: 'Continue', exact: true });
  if (await proceed.isVisible().catch(() => false)) {
    await proceed.click({ timeout: 2000 }).catch(error => {
      if (!/Timeout|closed|destroyed|navigation/i.test(error.message)) throw error;
    });
    confirmations++;
  }
}

async function predicate(edit, expected) {
  const deadline = Date.now() + plan.timeoutMs;
  while (Date.now() < deadline) {
    await confirmIsolatedDatabase();
    const visible = await page.evaluate(({ selector, property, expected }) => {
      const e = document.querySelector(selector);
      if (!e) return false;
      return property ? Math.abs(parseFloat(getComputedStyle(e)[property]) - expected) < 0.03 : e.textContent.trim() === expected;
    }, { selector: edit.selector, property: edit.property, expected }).catch(() => false);
    if (visible) {
      await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
      return;
    }
    await pause(50);
  }
  throw new Error('Browser predicate timeout after ' + plan.timeoutMs + 'ms.');
}

async function assets() {
  const status = await probe();
  const links = await page.locator('link[rel="stylesheet"]').evaluateAll(es => es.map(e => e.href));
  const expected = host === 'fast' ? 'catalog-fast' : 'output';
  const theme = links.find(link => new URL(link).pathname.startsWith('/css/') && path.basename(new URL(link).pathname).startsWith(expected));
  assert(theme, 'Requested theme link is absent.');
  const response = await page.request.get(theme);
  assert(response.ok(), 'Theme did not load.');
  const actual = await response.body();
  const physical = fs.readFileSync(host === 'fast' ? path.join(root, 'src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/wwwroot/css/catalog-fast.css') : productionCss);
  assert.equal(sha(actual), sha(physical), 'Served theme bytes differ from the chosen source.');
  if (host !== 'fullapp') assert.equal(await page.locator('html').getAttribute('data-asset-mode'), mode);
  if (host === 'fast') assert(!links.some(link => /^\/css\/output(?:\.|$)/.test(new URL(link).pathname)), 'Fast must not fall back to production CSS.');
  const styles = await page.evaluate(() => {
    const toolbar = document.querySelector('.agent-catalog-panel__toolbar');
    const scroll = document.querySelector('.agent-catalog-panel__agent-scroll');
    const tree = document.querySelector('.agent-catalog-panel__team-panel');
    const title = document.querySelector('.agent-catalog-panel__heading h2');
    const action = document.querySelector('[data-testid="agents-catalog-new"]');
    const rect = action.getBoundingClientRect();
    return {
      toolbarDisplay: getComputedStyle(toolbar).display, rowGap: getComputedStyle(toolbar).rowGap,
      cardOverflow: getComputedStyle(scroll).overflowY, treeOverflow: getComputedStyle(tree).overflowY,
      titleSize: getComputedStyle(title).fontSize, titleWeight: getComputedStyle(title).fontWeight,
      actionInViewport: rect.left >= 0 && rect.top >= 0 && rect.right <= innerWidth && rect.bottom <= innerHeight,
      iconsLoaded: document.fonts.check('24px "Material Symbols Rounded"'),
      avatarImages: [...document.querySelectorAll('[data-testid="agents-catalog-card-shell"] img')].filter(e => e.complete && e.naturalWidth > 0).length
    };
  });
  assert.equal(styles.toolbarDisplay, 'flex');
  assert.equal(styles.rowGap, '12px');
  assert.equal(styles.cardOverflow, 'auto');
  assert.equal(styles.treeOverflow, 'auto');
  assert(parseFloat(styles.titleSize) >= 20);
  assert(styles.actionInViewport && styles.iconsLoaded && styles.avatarImages > 0);
  log({ kind: 'asset-acceptance', status, themeSha256: sha(actual), themeBytes: actual.length, styles, links: links.map(x => new URL(x).pathname) });
}
async function acceptance() {
  await assets();
  await page.screenshot({ path: path.join(output, 'normal.jpeg'), type: 'jpeg', quality: 80 });
  if (host === 'fullapp') return;
  await page.getByTestId('agents-team-tree-team').first().click();
  await page.getByTestId('sandbox-intent').filter({ hasText: 'Selected team' }).waitFor();
  await page.getByTestId('agents-team-tree-all').click();
  await page.getByTestId('agents-catalog-card').first().click();
  await page.getByTestId('sandbox-intent').filter({ hasText: 'Selected agent' }).waitFor();
  await page.getByTestId('agents-catalog-new').click();
  await page.getByTestId('sandbox-intent').filter({ hasText: 'Open agent editor: New' }).waitFor();
  await page.getByTestId('agents-catalog-reset').hover();
  await page.getByRole('tooltip').filter({ hasText: 'Reset agent search' }).waitFor();
  await page.screenshot({ path: path.join(output, 'tooltip.jpeg'), type: 'jpeg', quality: 80 });
  await page.mouse.move(0, 0);
  await page.getByTestId('sandbox-loading').click();
  await page.getByText('Loading technical agents', { exact: true }).waitFor();
  const utilities = await page.getByText('Loading technical agents', { exact: true }).evaluate(e => ({ margin: getComputedStyle(e).marginTop, weight: getComputedStyle(e).fontWeight, size: getComputedStyle(e).fontSize }));
  assert.equal(utilities.margin, '4px');
  assert.equal(utilities.weight, '600');
  assert.equal(utilities.size, '18px');
  await page.screenshot({ path: path.join(output, 'loading.jpeg'), type: 'jpeg', quality: 80 });
  await page.getByTestId('sandbox-empty').click();
  await page.getByText('Create the first technical agent', { exact: true }).waitFor();
  await page.screenshot({ path: path.join(output, 'empty.jpeg'), type: 'jpeg', quality: 80 });
  await page.getByTestId('sandbox-card-states').click();
  await page.getByTestId('agent-favorite-toggle').click();
  await page.locator('[data-testid="agent-favorite-toggle"][aria-pressed="true"]').waitFor();
  await page.screenshot({ path: path.join(output, 'card-states.jpeg'), type: 'jpeg', quality: 80 });
  await page.getByTestId('sandbox-avatar-fallback').click();
  await page.getByText('CA', { exact: true }).waitFor();
  await page.screenshot({ path: path.join(output, 'avatar.jpeg'), type: 'jpeg', quality: 80 });
  await page.getByTestId('sandbox-normal').click();
  await page.getByTestId('agents-catalog-card').first().waitFor();
  log({ kind: 'acceptance-complete', assertions: ['real selection and New intents', 'tooltip', 'icon/font', 'card/tree scroll and actions', 'utility classes', 'loading', 'empty', 'avatar fallback'], utilities });
}
async function sdkCompletion(cursor) {
  const deadline = Date.now() + plan.sdkCompletionTimeoutMs;
  while (Date.now() < deadline) {
    const matched = events.slice(cursor).filter(e => /(?:C# and Razor|Static asset) changes applied in \d+ms\./.test(e.text));
    if (matched.length) return matched;
    await pause(50);
  }
  return [];
}

async function trial(edit, repetition) {
  const file = path.resolve(root, edit.path);
  assert(file.startsWith(root + path.sep));
  const original = fs.readFileSync(file);
  const before = original.toString('utf8');
  assert.equal(sha(original), edit.sourceSha256, 'Source differs from the frozen baseline.');
  assert.equal(before.split(edit.old).length, 2, 'Frozen edit must match exactly once.');
  assert(!before.includes(edit.replacement));
  const patched = Buffer.from(before.replace(edit.old, edit.replacement));
  await predicate(edit, edit.before);
  const ready = await probe();
  const navBefore = navigation;
  const confirmationsBefore = confirmations;
  const cursor = events.length;
  activeEdit = { file, original, patched };
  flush(file, patched);
  const t0 = clock();
  let result;
  try {
    await predicate(edit, edit.after);
    const firstVisible = clock();
    const sdk = await sdkCompletion(cursor);
    await predicate(edit, edit.after);
    const t1 = clock();
    const after = await probe();
    const classification = after.runtimePid !== ready.runtimePid ? 'restart' : navigation !== navBefore ? 'browser-reload' : sdk.length === 1 ? 'hot-reload' : 'missing-sdk-event';
    result = { kind: 'warm', host, editId: edit.id, category: edit.category, repetition, classification,
      success: ['hot-reload', 'browser-reload', 'restart'].includes(classification), sourceSha256: sha(original),
      patchSha256: sha(patched), flushNs: t0.toString(), firstVisibleNs: firstVisible.toString(), firstVisibleMs: Number(firstVisible - t0) / 1e6, visibleNs: t1.toString(), elapsedMs: Number(t1 - t0) / 1e6,
      ready, after, navigationCount: navigation - navBefore, databaseConfirmations: confirmations - confirmationsBefore, sdkEvents: sdk.map(e => ({ sequence: e.sequence, text: e.text, ns: e.ns,
        milliseconds: Number(e.text.match(/applied in (\d+)ms/)[1]) })) };
    assert.equal(after.ownerId, runId);
    if (host !== 'fullapp') assert.equal(after.assetMode, mode);
  } catch (error) {
    result = { kind: 'warm', host, editId: edit.id, category: edit.category, repetition, success: false,
      classification: /Timeout/i.test(error.message) ? 'timeout' : 'harness-failure', error: safeError(error), sourceSha256: sha(original),
      flushNs: t0.toString(), elapsedMs: Number(clock() - t0) / 1e6, ready };
  } finally {
    assert.deepEqual(fs.readFileSync(file), patched, 'Concurrent edit: refusing to overwrite.');
    const undoConfirmations = confirmations;
    const undoCursor = events.length;
    flush(file, original);
    try { await predicate(edit, edit.before); result.undoSdkEvents = await sdkCompletion(undoCursor); await predicate(edit, edit.before); result.undoVisible = result.undoSdkEvents.length === 1; } catch (error) { result.undoVisible = false; result.undoError = safeError(error); }
    assert.deepEqual(fs.readFileSync(file), original);
    result.undoSha256 = sha(fs.readFileSync(file));
    result.undoDatabaseConfirmations = confirmations - undoConfirmations;
    activeEdit = null;
    if (host === 'fast') assert.deepEqual(fs.readFileSync(productionCss), productionBefore, 'Fast changed production CSS.');
    log(result);
  }
  return result;
}
(async () => {
  assert.equal(sha(fs.readFileSync(__filename)), plan.harnessSha256, 'Harness differs from the frozen protocol.');
  const env = { ...process.env };
  if (host === 'fullapp') {
    const fixturePath = path.resolve(args.environment || path.join(root, '.mcp-state/catalog-data/environment.json'));
    assert(fixturePath.startsWith(path.join(root, '.mcp-state') + path.sep), 'Only an ignored isolated fixture environment is accepted.');
    const fixture = JSON.parse(fs.readFileSync(fixturePath, 'utf8'));
    assert(/Database=cditall_catalog_measurement_/.test(fixture.Database__ConnectionString), 'Expected isolated catalog measurement database.');
    Object.assign(env, fixture);
    env.DevelopmentManager__TuningModeEnabled = 'false';
  }
  Object.assign(env, { ASPNETCORE_ENVIRONMENT: 'Development', DOTNET_ENVIRONMENT: 'Development',
    ASPNETCORE_URLS: base, DOTNET_CLI_UI_LANGUAGE: 'en', DOTNET_WATCH_SUPPRESS_EMOJIS: '1',
    DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER: '1', DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH: '0',
    Logging__LogLevel__Default: 'Warning', CanDoItAllMcpOwnerKind: 'DirectCatalogBenchmark', CanDoItAllMcpOwnerId: runId });
  if (host !== 'fullapp') env.CatalogAssetMode = mode;
  const cssInput = host === 'fast' ? 'Tailwind/catalog-fast.css' : 'Tailwind/input.css';
  const cssOutput = host === 'fast' ? 'src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/wwwroot/css/catalog-fast.css' : 'src/App/CanDoItAll.Web/wwwroot/css/output.css';
  start('tailwind', process.execPath, [path.join(root, 'Tailwind/node_modules/@tailwindcss/cli/dist/index.mjs'),
    '-i', path.join(root, cssInput), '-o', path.join(root, cssOutput), '--watch=always'], env);
  const project = host === 'fullapp' ? 'src/App/CanDoItAll.Web' : 'src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox';
  const launch = host === 'fullapp' ? ['--no-launch-profile'] : ['--launch-profile', host === 'fast' ? 'Catalog sandbox Fast' : 'Catalog sandbox'];
  const watchArgs = ['watch', '--verbose', '--non-interactive', '--project', project, ...launch, '--property:CatalogAssetMode=' + mode];
  const watcher = start('dotnet-watch', 'dotnet', watchArgs, env);
  log({ kind: 'protocol', runId, planSha256: sha(fs.readFileSync(path.join(bundle, 'plan/frozen-direct-edits.json'))),
    harnessSha256: sha(fs.readFileSync(__filename)), host, phase, mode: host === 'fullapp' ? 'Production' : mode, sdk: execFileSync('dotnet', ['--version']).toString().trim(),
    node: process.version, platform: process.platform, watchPid: watcher.pid, productionCssBefore: sha(productionBefore) });
  const ready = await waitReady();
  assert.equal(execFileSync('dotnet', ['--version']).toString().trim(), plan.environment.sdk);
  assert.equal(process.version, plan.environment.node);
  assert.equal(require(path.join(path.dirname(require.resolve(modulePath)), 'package.json')).version, plan.environment.playwright);
  browser = await chromium.launch({ headless: true });
  assert.equal(browser.version(), plan.environment.browser);
  page = await browser.newPage({ viewport: plan.viewport });
  page.on('framenavigated', frame => { if (frame === page.mainFrame()) navigation++; });
  page.on('pageerror', error => log({ kind: 'browser-error', error: safeError(error) }));
  await fixture();
  log({ kind: 'ready', ready, browser: browser.version(), productionCssAtReady: sha(fs.readFileSync(productionCss)) });
  if (phase === 'acceptance') await acceptance();
  else {
    await assets();
    for (const edit of plan.edits) {
      for (let repetition = 1; repetition <= plan.successfulRepetitionsPerEdit; repetition++) {
        const result = await trial(edit, repetition);
        if (!result.success || !result.undoVisible) throw new Error('Trial failed; retained explicitly. Review before collecting a replacement run.');
        await pause(750);
      }
    }
  }
  fs.writeFileSync(path.join(output, 'events.json'), JSON.stringify(events, null, 2));
  log({ kind: 'complete', host, phase, productionCssAfter: sha(fs.readFileSync(productionCss)) });
})().catch(async error => {
  if (page) {
    fs.writeFileSync(path.join(output, 'failed-dom.txt'), await page.locator('body').innerText().catch(() => 'Unavailable'));
    fs.writeFileSync(path.join(output, 'failed-controls.json'), JSON.stringify(await page.locator('[data-testid]').evaluateAll(es => es.map(e => ({id:e.getAttribute('data-testid'),tag:e.tagName}))).catch(() => [])));
    await page.screenshot({path:path.join(output, 'failed.jpeg'),type:'jpeg',quality:75}).catch(() => {});
  }
  log({ kind: 'failed', error: safeError(error) });
  fs.writeFileSync(path.join(output, 'events.json'), JSON.stringify(events, null, 2));
  process.exitCode = 1;
}).finally(async () => {
  if (activeEdit) {
    if (fs.readFileSync(activeEdit.file).equals(activeEdit.patched)) flush(activeEdit.file, activeEdit.original);
    else log({ kind: 'restoration-blocked', path: path.relative(root, activeEdit.file) });
  }
  if (browser) await browser.close();
  for (const child of [...children].reverse()) stop(child);
  log({ kind: 'stopped', ownedPids: children.map(c => c.pid), productionCssSha256: sha(fs.readFileSync(productionCss)) });
});
