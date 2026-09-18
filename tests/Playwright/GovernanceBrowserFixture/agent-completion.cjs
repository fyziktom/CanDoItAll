const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { spawn, execFileSync } = require('node:child_process');
const { chromium } = require(process.env.GOV_PLAYWRIGHT);
const root = path.resolve(process.argv[2]);
const mode = process.argv[3] || 'web';
const raw = path.join(root, '.artifacts/agent-completion-02');
const base = 'http://127.0.0.1:' + (mode === 'web' ? 5285 : mode === 'Parity' ? 5395 : 5396);
const control = 'http://127.0.0.1:17315/fixture/';
const result = { mode, viewport: { width: 1600, height: 1000 }, checks: [], errors: [] };
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
let child, browser, page;
let frames = 0;
let startupConfirmed = false;
async function until(test, label, timeout = 45000) {
    const end = Date.now() + timeout;
    while (Date.now() < end) {
        try {
            if (await test()) return;
        } catch {}
        assert.equal(child.exitCode, null, 'Owned fixture exited: ' + label);
        await pause(150);
    }
    throw Error('Timed out: ' + label);
}
const post = async (lane, value) => assert((await fetch(control + lane + '/' + value, { method: 'POST' })).ok);
const counters = async lane => (await fetch(control + lane)).json();
async function navigate(route, enhanced = false) {
    const before = frames;
    if (enhanced) {
        await page.evaluate(url => {
            const link = document.createElement('a');
            link.href = url;
            document.body.append(link);
            link.click();
            link.remove();
        }, base + route);
    } else {
        await page.goto(base + route);
    }
    await until(() => frames > before + (enhanced ? 0 : 2), 'interactive navigation');
    if (!enhanced) await pause(1000);
    if (mode === 'web') {
        const confirm = page.getByTestId('database-startup-continue');
        if (!startupConfirmed) {
            await confirm.waitFor({ state: 'visible' });
        }
        if (await confirm.isVisible()) {
            await confirm.click();
            await until(async () => await page.getByTestId('database-startup-modal').count() === 0, 'profile accepted');
            startupConfirmed = true;
        }
    }
}
async function fill(id, text, scope = page) {
    const input = scope.getByTestId(id);
    await input.fill(text);
    await input.press('Tab');
}
async function inspect(name, locator) {
    assert.equal(await page.locator('#editor-injected, #definitions-injected, #conversation-injected').count(), 0);
    const geometry = await locator.evaluate(element => {
        const r = element.getBoundingClientRect();
        return { width: r.width, scroll: element.scrollWidth, client: element.clientWidth };
    });
    assert(geometry.width > 0, name + ' is visible');
    assert(geometry.scroll <= geometry.client + 3, name + ' has no horizontal overflow');
    result.checks.push(name);
}
async function cancelEditor() {
    await page.getByTestId('llm-chat-definition-editor-cancel').click();
    await until(async () => await page.getByTestId('llm-chat-definition-editor-dialog').count() === 0 && !new URL(page.url()).searchParams.has('definitionId'), 'editor cancellation and route acknowledgement');
}
async function editor() {
    const a = '31000000-0000-0000-0000-000000000001';
    const b = '31000000-0000-0000-0000-000000000002';
    const dialog = page.getByTestId('llm-chat-definition-editor-dialog');
    await navigate('/agents?tab=simple-chats');
    await page.getByTestId('llm-chat-definition-create').click();
    await dialog.getByTestId('llm-chat-definition-name').waitFor();
    await inspect('Web editor New', dialog);
    await cancelEditor();
    await navigate('/agents?tab=simple-chats&definitionId=' + a, true);
    await until(async () => await dialog.getByTestId('llm-chat-definition-name').inputValue() === 'Research assistant', 'editor A');
    await navigate('/agents?tab=simple-chats&definitionId=' + b, true);
    await until(async () => await dialog.getByTestId('llm-chat-definition-name').inputValue() === 'Operations assistant', 'editor B');
    assert.equal(await dialog.count(), 1);
    await inspect('Web editor A to B', dialog);
    await fill('llm-chat-definition-name', 'Browser edited definition', dialog);
    await fill('llm-chat-definition-summary', 'Edited summary with <encoded> content', dialog);
    await dialog.getByTestId('llm-chat-definition-tags-input').fill('browser');
    await dialog.getByTestId('llm-chat-definition-tags-input').press('Enter');
    await dialog.getByTestId('llm-chat-definition-tab-runtime').click();
    await dialog.getByTestId('llm-chat-definition-provider').selectOption('0');
    await fill('llm-chat-definition-system-prompt', 'Synthetic edited prompt', dialog);
    await fill('llm-chat-definition-temperature', '0.3', dialog);
    await dialog.getByTestId('llm-chat-definition-thinking-effort').selectOption('1');
    await fill('llm-chat-definition-timeout', '45', dialog);
    await fill('llm-chat-definition-model-parameters', '{"seed":7}', dialog);
    await dialog.getByTestId('llm-chat-definition-tab-output').click();
    await dialog.getByTestId('llm-chat-definition-response-format').selectOption('2');
    await fill('llm-chat-definition-schema-name', 'browser_answer', dialog);
    await fill('llm-chat-definition-schema-description', 'Synthetic response schema', dialog);
    await fill('llm-chat-definition-schema-json', '{"type":"object"}', dialog);
    await fill('llm-chat-definition-revision-reason', 'Browser validation', dialog);
    const before = await counters('definitions');
    await dialog.getByTestId('llm-chat-definition-editor-save').click();
    await until(async () => await dialog.count() === 0, 'editor saved and closed');
    assert.equal((await counters('definitions')).saves, before.saves + 1);
    await navigate('/agents?tab=simple-chats&definitionId=' + b, true);
    await until(async () => await dialog.getByTestId('llm-chat-definition-name').inputValue() === 'Browser edited definition', 'accepted saved fields');
    await dialog.getByTestId('llm-chat-definition-tab-runtime').click();
    assert.equal(await dialog.getByTestId('llm-chat-definition-system-prompt').inputValue(), 'Synthetic edited prompt');
    await dialog.getByTestId('llm-chat-definition-tab-output').click();
    assert.equal(await dialog.getByTestId('llm-chat-definition-schema-name').inputValue(), 'browser_answer');
    await inspect('Web editor every section saved and reopened', dialog);
    await page.screenshot({ path: path.join(raw, 'editor.png') });
    await post('definitions', 'Conflict');
    await dialog.getByTestId('llm-chat-definition-editor-save').click();
    await dialog.getByTestId('llm-chat-definition-editor-reload').waitFor();
    await post('definitions', 'Normal');
    const read = (await counters('definitions')).editorReads;
    await dialog.getByTestId('llm-chat-definition-editor-reload').click();
    await until(async () => (await counters('definitions')).editorReads === read + 1, 'same-target conflict reload');
    await dialog.getByTestId('llm-chat-definition-status-active').click();
    await until(async () => await dialog.count() === 0, 'status closes accepted editor');
    assert.equal((await counters('definitions')).statusChanges, 1);
    result.checks.push('Web conflict reload and status transition');
    await post('definitions', 'ProviderFailure');
    await navigate('/agents?tab=simple-chats&definitionId=' + a, true);
    await dialog.getByTestId('llm-chat-definition-tab-runtime').click();
    await until(async () => (await dialog.innerText()).includes('provider'), 'provider partial state');
    assert(await dialog.getByTestId('llm-chat-definition-system-prompt').isEditable());
    await inspect('Web editor provider partial failure', dialog);
    await cancelEditor();
    await post('definitions', 'Normal');
    await navigate('/agents?tab=simple-chats&definitionId=31000000-0000-0000-0000-000000000003', true);
    await dialog.getByTestId('llm-chat-definition-name').waitFor();
    await inspect('Web editor adversarial values', dialog);
    await cancelEditor();
}
async function conversations() {
    const a = '35000000-0000-0000-0000-000000000001';
    const b = '35000000-0000-0000-0000-000000000002';
    const route = id => '/agents?tab=simple-chats&simpleChatView=conversations' + (id ? '&conversationId=' + id : '');
    const workspace = page.getByTestId('llm-chat-conversation-workspace');
    await navigate(route());
    await page.getByTestId('llm-chat-selected-title').waitFor();
    await inspect('Web conversation initial list and default selection', workspace);
    await page.getByTestId('llm-chat-thread-load-more').click();
    await until(async () => (await workspace.innerText()).includes('Browser conversation 26'), 'conversation paging');
    await navigate(route(a), true);
    await navigate(route(b), true);
    await until(async () => await page.getByTestId('llm-chat-selected-title').innerText() === 'Browser conversation B', 'conversation A to B');
    await page.getByTestId('llm-chat-transcript-load-more').click();
    await until(async () => (await workspace.innerText()).includes('Paged canonical answer'), 'transcript paging');
    await fill('llm-chat-prompt', 'Synthetic browser question');
    const count = (await counters('completion')).sends;
    await page.getByTestId('llm-chat-send').click();
    await workspace.locator('[data-testid="conversation-message"][data-state="pending"]').waitFor();
    assert.equal((await counters('completion')).sends, count + 1);
    await post('completion', 'Stream');
    await until(async () => (await workspace.innerText()).includes('Synthetic streamed answer'), 'synthetic stream');
    await page.getByTestId('llm-chat-operation-cancel').click();
    await inspect('Web conversation route paging composer pending stream and Cancel', workspace);
    await navigate(route(a), true);
    await page.getByTestId('llm-chat-new').click();
    const start = page.getByTestId('llm-chat-start-dialog');
    await start.getByTestId('llm-chat-start-definition-31000000-0000-0000-0000-000000000001').click();
    await fill('llm-chat-start-title', 'Created browser chat', start);
    await start.getByTestId('llm-chat-start-confirm').click();
    await until(async () => await page.getByTestId('llm-chat-selected-title').innerText() === 'Created browser chat', 'new conversation');
    const created = new URL(page.url()).searchParams.get('conversationId');
    assert(created);
    await page.getByTestId('llm-chat-rename-' + created).click();
    await fill('llm-chat-rename-title', 'Renamed browser chat');
    await page.getByTestId('llm-chat-rename-confirm').click();
    await until(async () => await page.getByTestId('llm-chat-selected-title').innerText() === 'Renamed browser chat', 'renamed conversation');
    await page.getByTestId('llm-chat-archive-' + created).click();
    const archive = page.getByTestId('llm-chat-archive-dialog');
    await archive.locator('input').fill('Renamed browser chat');
    await archive.locator('input').press('Tab');
    await archive.getByRole('button', { name: 'Archive', exact: true }).click();
    await until(async () => await archive.count() === 0, 'archived conversation');
    assert(await page.getByTestId('llm-chat-send').isDisabled());
    await inspect('Web New Rename Archive', workspace);
    await post('completion', 'Recovery');
    await navigate(route(created));
    await page.getByTestId('llm-chat-operation-reconcile').click();
    await page.getByTestId('llm-chat-operation-abandon').click();
    await until(async () => await page.getByTestId('llm-chat-operation-abandon').count() === 0, 'recovery abandoned');
    result.checks.push('Web recovery Reconcile and Abandon');
    await page.getByTestId('shell-agent-chats-action').click();
    await page.getByTestId('conversation-shell-filter-chats').click();
    await page.getByTestId('floating-simple-chat-new-31000000000000000000000000000001').click();
    const focused = page.getByTestId('floating-simple-chat-content');
    await focused.waitFor();
    assert.equal(await focused.getByTestId('llm-chat-thread-search').count(), 0);
    await inspect('Web focused floating conversation', focused);
    await page.screenshot({ path: path.join(raw, 'conversation.png') });
}
async function settings() {
    await post('completion', 'Normal');
    await navigate('/agents?tab=voice');
    const voice = page.getByTestId('agents-voice-settings');
    await voice.waitFor();
    await voice.getByTestId('agents-voice-stt-enabled').check();
    await voice.getByTestId('agents-voice-tts-enabled').check();
    const provider = '34000000-0000-0000-0000-000000000001';
    await voice.getByTestId('agents-voice-stt-provider').selectOption(provider);
    await voice.getByTestId('agents-voice-tts-provider').selectOption(provider);
    for (const [id, text] of [['stt-model', 'synthetic-stt'], ['stt-language', 'en'], ['stt-prompt', 'Synthetic prompt'], ['tts-model', 'synthetic-tts'], ['sample-text', 'Synthetic browser sample']]) {
        await fill('agents-voice-' + id, text, voice);
    }
    await voice.getByTestId('agents-voice-default-voice').selectOption('alloy');
    await voice.getByTestId('agents-voice-format').selectOption('wav');
    await voice.getByTestId('agents-voice-save').click();
    await until(async () => (await voice.innerText()).includes('Saved'), 'voice saved');
    await page.evaluate(() => {
        window.fixturePlaybackCount = 0;
        window.CanDoItAll.agentFramework.voice.playAudio = async () => {
            window.fixturePlaybackCount++;
            if (window.fixturePlaybackFails) throw Error('Synthetic playback failure');
        };
    });
    await voice.getByTestId('agents-voice-play-sample').click();
    await until(async () => (await voice.innerText()).includes('Sample played'), 'synthetic playback');
    assert.equal(await page.evaluate(() => window.fixturePlaybackCount), 1);
    await page.evaluate(() => window.fixturePlaybackFails = true);
    await voice.getByTestId('agents-voice-play-sample').click();
    await until(async () => (await voice.innerText()).includes('Saved. Sample playback failed'), 'saved playback failure');
    await post('completion', 'VoiceSynthesisFailure');
    await voice.getByTestId('agents-voice-play-sample').click();
    await until(async () => (await voice.innerText()).includes('Saved. Sample generation failed'), 'saved synthesis failure');
    await post('completion', 'VoiceSaveFailure');
    const samples = (await counters('completion')).samples;
    await voice.getByTestId('agents-voice-play-sample').click();
    await until(async () => (await voice.innerText()).includes('could not be saved'), 'voice save failure');
    assert.equal((await counters('completion')).samples, samples);
    await inspect('Web voice save sample and independent failure outcomes', voice);
    await post('completion', 'VoiceProvidersFailure');
    await navigate('/agents?tab=voice');
    await page.getByTestId('agents-voice-provider-warning').waitFor();
    assert.equal(await voice.getByTestId('agents-voice-tts-provider').inputValue(), provider);
    result.checks.push('Web voice provider partial failure preserves selection');
    await post('completion', 'Normal');
    await navigate('/agents?tab=floating-chat');
    const floating = page.getByTestId('floating-agent-chat-settings');
    await floating.waitFor();
    for (const [id, text] of [['retention', '25'], ['maximum-active', '9'], ['maximum-prepared', '0'], ['prepared-retention', '30']]) {
        await fill('floating-agent-chat-' + id, text, floating);
    }
    await floating.getByTestId('floating-agent-chat-adaptive').uncheck();
    await page.getByTestId('floating-agent-chat-settings-save').click();
    await until(async () => (await page.locator('main').innerText()).includes('Saved'), 'floating saved');
    await post('completion', 'FloatingApplyWarning');
    const saves = (await counters('completion')).floatingSaves;
    await page.getByTestId('floating-agent-chat-settings-save').click();
    await until(async () => (await page.locator('main').innerText()).includes('Saved with runtime application warning'), 'saved coordinator warning');
    assert.equal((await counters('completion')).floatingSaves, saves + 1);
    await inspect('Web floating all fields Save and coordinator warning', floating);
}
async function sandbox() {
    for (const [specimen, selector, rootId] of [
        ['simple-chat-definition-editor', '#definition-editor-scenario', 'sandbox-definition-editor-specimen'],
        ['simple-chat-conversation', '#conversation-workspace-scenario', 'sandbox-conversation-workspace-specimen'],
        ['voice-settings', '#agent-settings-scenario', 'sandbox-agent-settings-specimen'],
        ['floating-chat-settings', '#agent-settings-scenario', 'sandbox-agent-settings-specimen']
    ]) {
        await navigate('/agents?specimen=' + specimen);
        const select = page.locator(selector);
        await select.waitFor();
        const scenarios = await select.locator('option').evaluateAll(items => items.map(item => item.value));
        assert.equal(scenarios.length, specimen.includes('editor') ? 8 : specimen.includes('conversation') ? 10 : 4);
        for (const scenario of scenarios) {
            await select.selectOption(scenario);
            await until(async () => await page.getByTestId(rootId).getAttribute('data-scenario') === scenario, 'accepted sandbox scenario ' + scenario);
            const surface = specimen.includes('editor') ? page.getByTestId('llm-chat-definition-editor-dialog') : page.getByTestId(rootId);
            await inspect(mode + ' ' + specimen + ' ' + scenario, surface);
            if (specimen.includes('editor') && scenario !== 'Loading' && scenario !== 'Denied') {
                await page.getByTestId('llm-chat-definition-tab-runtime').click();
                await page.getByTestId('llm-chat-definition-system-prompt').waitFor();
                await page.getByTestId('llm-chat-definition-tab-output').click();
            }
        }
        if (specimen.includes('settings')) {
            await select.selectOption('Ready');
            await page.getByTestId(specimen === 'voice-settings' ? 'agents-voice-save' : 'floating-agent-chat-settings-save').click();
            await until(async () => await page.getByTestId('sandbox-intent').innerText() === 'Save', 'controlled settings Save');
        }
    }
}
(async () => {
    fs.mkdirSync(raw, { recursive: true });
    const log = fs.createWriteStream(path.join(raw, 'browser-' + mode + '.log'));
    const args = mode === 'web' ? [path.join(root, 'tests/Playwright/GovernanceBrowserFixture/bin/Release/net10.0/GovernanceBrowserFixture.dll'), 'serve-completion', root]
        : [path.join(root, 'src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/bin', mode, 'Release/net10.0/CanDoItAll.AgentFramework.UiSandbox.dll')];
    child = spawn('dotnet', args, { cwd: mode === 'web' ? root : path.join(root, 'src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox'),
        env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', DOTNET_ENVIRONMENT: 'Development', ASPNETCORE_URLS: base }, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
    child.stdout.pipe(log);
    child.stderr.pipe(log);
    await until(async () => (await fetch(base + '/agents')).ok, 'host ready', 120000);
    browser = await chromium.launch({ headless: true });
    page = await browser.newPage({ viewport: result.viewport });
    page.setDefaultTimeout(20000);
    page.on('pageerror', error => result.errors.push(error.message));
    page.on('websocket', socket => {
        if (new URL(socket.url()).pathname.endsWith('/_blazor')) socket.on('framereceived', () => frames++);
    });
    if (mode === 'web') {
        await editor();
        await conversations();
        await settings();
    } else {
        await sandbox();
    }
    assert.deepEqual(result.errors, []);
})().catch(async error => {
    result.failure = error.stack;
    result.failedText = await page?.locator('body').innerText({ timeout: 1000 }).then(text => text.slice(-5000)).catch(() => 'Unavailable');
    process.exitCode = 1;
}).finally(async () => {
    await browser?.close();
    if (mode === 'web' && child?.exitCode === null) {
        await fetch(control + 'stop', { method: 'POST' }).catch(() => {});
        await pause(1000);
    }
    if (child?.exitCode === null) {
        if (process.platform === 'win32') execFileSync('taskkill', ['/PID', String(child.pid), '/T', '/F'], { windowsHide: true, stdio: 'ignore' });
        else child.kill('SIGTERM');
    }
    fs.writeFileSync(path.join(raw, 'browser-' + mode + '.json'), JSON.stringify(result, null, 2));
    console.log(JSON.stringify(result));
});
