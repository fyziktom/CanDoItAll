const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { spawn, execFileSync } = require('node:child_process');
const { chromium } = require(process.env.GOV_PLAYWRIGHT);
const root = path.resolve(process.argv[2]);
const mode = process.argv[3] || 'web';
const workflowScope = process.argv.includes('--workflows');
const surfaceName = workflowScope ? 'workflows' : 'chat';
const raw = path.join(root, '.artifacts/agent-final-closure');
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
        await pause(100);
    }
    throw Error('Timed out: ' + label);
}
const post = async value => assert((await fetch(control + 'chat/' + value, { method: 'POST' })).ok);
const counters = async () => (await fetch(control + 'chat')).json();
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
        if (!startupConfirmed) await confirm.waitFor({ state: 'visible' });
        if (await confirm.isVisible()) {
            await confirm.click();
            await page.getByTestId('database-startup-modal').waitFor({ state: 'hidden' });
            startupConfirmed = true;
        }
    }
}
async function inspect(name, locator) {
    const geometry = await locator.evaluate(element => {
        const r = element.getBoundingClientRect();
        return { width: r.width, scroll: element.scrollWidth, client: element.clientWidth };
    });
    assert(geometry.width > 0, name + ' is visible');
    assert(geometry.scroll <= geometry.client + 3, name + ' has no horizontal overflow');
    result.checks.push(name);
}
async function send(text, scope = page) {
    await scope.getByTestId('chat-prompt-input').fill(text);
    await scope.getByTestId('chat-prompt-input').press('Tab');
    await scope.getByTestId('chat-send-button').click();
}
async function inspectChatRail(panel) {
    const rail = panel.locator('.agents-chat-left-rail');
    for (const name of ['Switch Agent', 'Refresh', 'New thread']) {
        const box = await rail.getByRole('button', { name, exact: true }).boundingBox();
        assert(box && box.width >= 60 && box.height <= 48, 'Thread rail action remains readable: ' + name);
    }
}
async function web() {
    const a = '83cbd161-4bcb-45f9-9cb7-130000000001';
    const b = '83cbd161-4bcb-45f9-9cb7-130000000002';
    const panel = page.getByTestId('agents-chat-panel');
    await navigate('/agents?tab=chat&agentId=' + a);
    await panel.getByTestId('chat-workspace-panel').getByText('Initial fixture answer', { exact: true }).waitFor();
    await inspect('Web full chat and transcript', panel);
    await inspectChatRail(panel);
    if (process.argv.includes('--extras')) {
        await extras();
        return;
    }
    if (process.argv.includes('--inspect')) {
        result.snapshot = await page.locator('body').innerText();
        return;
    }
    await panel.getByTestId('agent-thread-card').filter({ hasText: 'Fixture thread A2' }).click();
    await until(async () => (await panel.locator('.chat-thread-title-edit').innerText()).includes('Fixture thread A2'), 'S1 to S2');
    await panel.getByRole('button', { name: 'New thread', exact: true }).click();
    await until(async () => (await counters()).creates === 1, 'new thread');
    await panel.locator('.chat-thread-title-edit .cda-editable__action').click();
    await panel.locator('.chat-thread-title-edit input').fill('Browser renamed thread');
    await panel.locator('.chat-thread-title-edit .cda-editable__action--save').click();
    await until(async () => (await counters()).renames === 1, 'rename');
    await send('Browser synthetic prompt');
    await panel.getByTestId('chat-workspace-panel').getByText('Synthetic answer: Browser synthetic prompt', { exact: true }).waitFor();
    await panel.getByTestId('agent-execution-activity-phase').filter({ hasText: 'Completed' }).waitFor();
    await panel.getByTestId('agents-chat-open-runtime-details').click();
    const runtime = page.getByTestId('agent-runtime-details-dialog');
    await runtime.getByText('Fixture execution', { exact: true }).waitFor();
    await inspect('Web runtime details and metrics', runtime);
    await page.screenshot({ path: path.join(raw, 'chat.png') });
    await runtime.getByRole('button', { name: /close/i }).first().click();
    await panel.getByTestId('chat-execution-summary').click();
    await page.getByTestId('agent-execution-log-dialog').waitFor();
    await page.getByTestId('agent-execution-log-dialog').getByRole('button', { name: /close/i }).first().click();
    await panel.getByTestId('chat-attachment-button').click();
    await panel.getByText('artifacts/fixture.txt', { exact: true }).waitFor();
    await panel.locator('input[type=file]').setInputFiles({ name: 'fixture.png', mimeType: 'image/png', buffer: Buffer.from([1, 2, 3]) });
    await panel.getByText('uploads/fixture.png', { exact: true }).waitFor();
    result.checks.push('Web thread selection, create, rename, Send, execution history, artifacts and upload');
    await page.evaluate(() => {
        const voice = window.CanDoItAll.agentFramework.voice;
        window.fixtureVoiceCalls = [];
        for (const method of ['startRecordingForOwner', 'disposeOwner', 'clearAudioQueueForOwner', 'enqueueAudioForOwner']) {
            voice[method] = async owner => window.fixtureVoiceCalls.push({ method, owner });
        }
        voice.stopRecordingForOwner = async owner => ({ base64: 'AQID', contentType: 'audio/webm', fileName: 'fixture.webm' });
    });
    await panel.getByTestId('chat-voice-mode-button').click();
    await panel.getByTestId('chat-voice-record-button').click();
    await panel.getByText('Recording', { exact: true }).waitFor();
    await panel.getByTestId('chat-voice-record-button').click();
    await until(async () => (await counters()).transcriptions === 1, 'transcription');
    await until(async () => !(await panel.getByTestId('chat-voice-speak-button').isDisabled()), 'voice send completed');
    await panel.getByTestId('chat-voice-speak-button').click();
    await until(async () => (await counters()).speech >= 1, 'speaking');
    result.checks.push('Web synthetic recording, transcription and speech');
    for (const action of ['decisions', 'chat-approve-conversation-button', 'chat-approve-once-button', 'chat-reject-button']) {
        await post('Approvals');
        await panel.getByRole('button', { name: 'Refresh', exact: true }).click();
        await panel.getByTestId('chat-approval-approve-fixture-read').waitFor();
        await post('Normal');
        const before = (await counters()).approvals;
        if (action === 'decisions') {
            await panel.getByTestId('chat-approval-approve-fixture-read').click();
            await panel.getByTestId('chat-approval-reject-fixture-write').click();
            await panel.getByTestId('chat-submit-approval-decisions-button').click();
        } else {
            await panel.getByTestId(action).click();
        }
        await until(async () => (await counters()).approvals === before + 1, 'approval once');
        await panel.getByTestId('chat-approval-approve-fixture-read').waitFor({ state: 'hidden' });
    }
    result.checks.push('Web individual approvals, remaining, approve once and reject once');
    await post('Failed');
    await send('Persist this synthetic failure');
    await panel.getByText('Failed', { exact: true }).first().waitFor();
    result.checks.push('Web durable failed execution');
    await post('Normal');
    await panel.getByTestId('agent-switch-button').click();
    const chooser = page.getByTestId('agent-switch-dialog-modal');
    await chooser.getByTestId('agent-favorite-toggle').first().click();
    await until(async () => (await counters()).favoriteSaves === 1, 'favorite');
    await chooser.getByTestId('agent-switch-card').filter({ hasText: 'Governance agent B' }).click();
    await panel.getByTestId('agent-thread-selected-agent').filter({ hasText: 'Governance agent B' }).waitFor();
    result.checks.push('Web favorite and agent switch');
    await post('HoldWorkspace');
    await navigate('/agents?tab=chat&agentId=' + a, true);
    await panel.getByText('Loading agent workspace', { exact: true }).waitFor();
    await navigate('/agents?tab=chat&agentId=' + b, true);
    await panel.getByTestId('agent-thread-selected-agent').filter({ hasText: 'Governance agent B' }).waitFor();
    await fetch(control + 'chat-release', { method: 'POST' });
    await pause(250);
    assert((await panel.innerText()).includes('Fixture thread B'));
    await post('HoldExecution');
    await send('Detached browser execution');
    await navigate('/agents?tab=chat&agentId=' + a, true);
    await panel.getByTestId('agent-thread-selected-agent').filter({ hasText: 'Governance agent A' }).waitFor();
    await fetch(control + 'chat-release', { method: 'POST' });
    await pause(250);
    assert(!(await panel.innerText()).includes('Detached browser execution'));
    result.checks.push('Web stale workspace and detached execution');
    await post('Long');
    await panel.getByRole('button', { name: 'Refresh', exact: true }).click();
    await panel.getByText(/Full legitimate message <encoded>/).first().waitFor();
    await inspect('Web long encoded message', panel);
    await post('Normal');
    await extras();
    result.counters = await counters();
}
async function extras() {
    const createsBefore = (await counters()).creates;
    await page.getByRole('button', { name: 'Insert from prompt gallery', exact: true }).click();
    const gallery = page.getByTestId('prompt-gallery-picker-dialog');
    await gallery.getByRole('button', { name: 'Insert', exact: true }).first().click();
    await gallery.waitFor({ state: 'hidden' });
    const warning = page.getByTestId('prompt-gallery-chat-compatibility-dialog');
    await until(async () => (await page.getByTestId('chat-prompt-input').inputValue()).length > 0 || await warning.isVisible(), 'gallery selection');
    if (await warning.isVisible()) await warning.getByRole('button', { name: 'Insert anyway', exact: true }).click();
    assert((await page.getByTestId('chat-prompt-input').inputValue()).length > 0);
    result.checks.push('Web Prompt Gallery insertion');
    await page.getByTestId('shell-agent-chats-action').click();
    const catalog = page.getByTestId('floating-agent-catalog-window');
    await catalog.getByTestId('floating-agent-chat-agent-list').waitFor();
    await catalog.getByRole('button', { name: 'Agents', exact: true }).click();
    await catalog.locator('.agent-compact-list-item__select').filter({ hasText: 'Governance agent A' }).dblclick();
    const floating = page.getByTestId('floating-agent-chat-content');
    await floating.getByTestId('chat-prompt-input').waitFor();
    assert.equal(await floating.locator('.agents-chat-left-rail').count(), 0);
    await inspect('Web focused floating Chat', floating);
    await send('Floating synthetic prompt', floating);
    await floating.getByText('Synthetic answer: Floating synthetic prompt', { exact: true }).waitFor();
    await floating.getByTestId('agents-chat-focused-new-thread').click();
    await until(async () => (await counters()).creates === createsBefore + 2, 'floating new thread');
    result.checks.push('Web floating Send, operation coordination and New thread');
}
const workflowState = async () => (await fetch(control + 'workflows')).json();
const workflowMode = async value => assert((await fetch(control + 'workflows/' + value, { method: 'POST' })).ok);
const releaseWorkflows = async () => assert((await fetch(control + 'workflows-release', { method: 'POST' })).ok);
async function workflowTab(name) {
    await page.getByTestId('workflows-tab-' + name).click();
    await until(async () => await page.getByTestId('workflows-tab-' + name).getAttribute('aria-selected') === 'true', 'workflow tab ' + name);
}
async function closeWorkflowDialog(testId) {
    const dialog = page.getByTestId(testId);
    if (await dialog.isVisible()) {
        await dialog.getByRole('button', { name: 'Close', exact: true }).first().click();
        await dialog.waitFor({ state: 'hidden' });
    }
}
async function workflowWeb() {
    const a = '53000000-0000-0000-0000-000000000001';
    const b = '53000000-0000-0000-0000-000000000002';
    const project = '53000000-0000-0000-0000-000000000003';
    const run = '53000000-0000-0000-0000-000000000101';
    const route = id => '/agents/workflows?projectId=' + project + '&workflowId=' + id;
    await navigate(route(a));
    await page.getByTestId('workflow-overview-content').waitFor();
    await until(async () => !(await page.getByTestId('workflows-curator-open').isDisabled()), 'managed Curator ready');
    await inspect('Web workflow dashboard', page.getByTestId('workflow-overview-content'));
    await workflowMode('HoldCurator');
    await page.getByTestId('workflows-curator-open').click();
    await until(async () => (await workflowState()).curatorStarts === 1, 'one Curator launch');
    await navigate(route(b), true);
    await page.getByTestId('workflow-overview-content').waitFor();
    assert(await page.getByTestId('workflows-curator-open').isDisabled());
    await releaseWorkflows();
    await workflowMode('Normal');
    await until(async () => !(await page.getByTestId('workflows-curator-open').isDisabled()), 'Curator flight released');
    assert.equal((await workflowState()).curatorStarts, 1);
    result.checks.push('Web exact Curator identity and one flight across route change');
    await workflowTab('workflows');
    await until(async () => (await page.getByTestId('workflows-detail').innerText()).includes('Browser workflow B'), 'exact B route');
    await page.goBack();
    await until(async () => (await page.getByTestId('workflows-detail').innerText()).includes('Browser workflow A'), 'browser Back applies A');
    await page.goForward();
    await until(async () => (await page.getByTestId('workflows-detail').innerText()).includes('Browser workflow B'), 'browser Forward applies B');
    result.checks.push('Web project/workflow identity and browser back/forward');
    await workflowMode('HoldDefinition');
    const readsBefore = (await workflowState()).definitionReads;
    await navigate(route(a), true);
    await until(async () => (await workflowState()).definitionReads > readsBefore, 'held A definition');
    await workflowMode('Normal');
    await navigate(route(b), true);
    await until(async () => (await page.getByTestId('workflows-detail').innerText()).includes('Browser workflow B'), 'B accepted before A release');
    await releaseWorkflows();
    await pause(300);
    assert((await page.getByTestId('workflows-detail').innerText()).includes('Browser workflow B'));
    result.checks.push('Web delayed A definition cannot replace B');
    await navigate(route(a), true);
    await page.getByTestId('workflows-publish').waitFor();
    await page.getByTestId('workflows-publish').click();
    await until(async () => (await workflowState()).publishes === 1, 'Publish');
    await page.getByTestId('workflows-publish').waitFor({ state: 'hidden' });
    await page.getByTestId('workflows-open-template-catalogue').click();
    const templates = page.getByTestId('workflows-template-catalogue-dialog');
    await templates.getByTestId('workflows-template-catalogue-item').first().waitFor();
    await templates.getByTestId('workflows-template-catalogue-search').fill('review');
    await templates.getByTestId('workflows-template-catalogue-search').press('Tab');
    await until(async () => await templates.getByTestId('workflows-template-catalogue-item').count() > 0, 'template search');
    await templates.getByTestId('workflows-template-preview').first().click();
    await page.getByTestId('workflows-template-preview-canvas').waitFor();
    const beforeTemplate = (await workflowState()).saves;
    await page.getByTestId('workflows-template-add-draft').click();
    await until(async () => (await workflowState()).saves > beforeTemplate, 'template draft');
    await templates.waitFor({ state: 'hidden' });
    result.checks.push('Web Publish, template search, preview canvas and add draft');
    await page.getByTestId('workflows-create-starter').click();
    await until(async () => (await workflowState()).saves > beforeTemplate + 1, 'starter creation');
    await workflowTab('editor');
    await page.getByTestId('workflow-canvas-editor').waitFor();
    await page.getByTestId('workflow-canvas-toggle-components').click();
    await page.getByTestId('workflow-canvas-component').first().waitFor();
    await page.getByTestId('workflow-canvas-validate').click();
    const beforeCanvas = (await workflowState()).saves;
    await page.getByTestId('workflow-canvas-save').click();
    await until(async () => (await workflowState()).saves > beforeCanvas, 'canvas save');
    result.checks.push('Web starter, original Canvas editor, component library, validation and save');
    await navigate(route(a) + '&runId=' + run, true);
    await workflowTab('history');
    await page.getByTestId('workflows-run-item').first().waitFor();
    assert.equal(await page.getByTestId('workflows-run-item').count(), 8);
    assert.equal(await page.getByTestId('workflows-run-event').count(), 8);
    assert(!(await page.locator('body').innerText()).includes('FULL_EVENT_TAIL'));
    await page.getByTestId('workflows-event-detail').first().click();
    await page.getByTestId('workflows-event-detail-dialog').getByText(/FULL_EVENT_TAIL/).first().waitFor();
    await closeWorkflowDialog('workflows-event-detail-dialog');
    await page.getByTestId('workflows-run-detail').first().click();
    await page.getByTestId('workflows-run-detail-artifact').first().waitFor();
    await closeWorkflowDialog('workflows-run-detail-dialog');
    await page.getByTestId('workflows-event-page-next').click();
    await until(async () => (await page.getByTestId('workflows-event-pager').innerText()).includes('Page 2'), 'event page 2');
    await page.getByTestId('workflows-run-page-next').click();
    await until(async () => (await page.getByTestId('workflows-run-pager').innerText()).includes('Page 2'), 'run page 2');
    result.checks.push('Web bounded run/event paging, full details and artifacts');
    await workflowMode('HoldRun');
    const beforeRunRead = (await workflowState()).runReads;
    await page.getByTestId('workflows-refresh').click();
    await until(async () => (await workflowState()).runReads > beforeRunRead, 'held R1');
    await workflowMode('Normal');
    const secondRun = '53000000-0000-0000-0000-000000000102';
    await navigate(route(a) + '&runId=' + secondRun, true);
    await page.locator('.workflows-run-item--selected').getByText('Browser run A2', { exact: true }).waitFor();
    await releaseWorkflows();
    await page.goBack();
    await page.locator('.workflows-run-item--selected').getByText('Browser run A1', { exact: true }).waitFor();
    await page.goForward();
    await page.locator('.workflows-run-item--selected').getByText('Browser run A2', { exact: true }).waitFor();
    result.checks.push('Web delayed R1 cannot replace R2 and run route back/forward');
    await workflowMode('HumanInput');
    await navigate(route(a) + '&runId=' + run, true);
    await page.getByTestId('workflows-refresh').click();
    await page.getByTestId('workflows-pending-response').waitFor();
    await page.getByTestId('workflows-pending-response').fill('{"approved":false,"note":"captured browser response"}');
    await page.getByTestId('workflows-pending-response').press('Tab');
    await page.getByTestId('workflows-respond-request').click();
    await until(async () => (await workflowState()).responses === 1, 'human response');
    assert((await workflowState()).lastResponse.includes('captured browser response'));
    await page.getByTestId('workflows-cancel-run').click();
    await until(async () => (await workflowState()).cancellations === 1, 'cancel run');
    await until(async () => (await page.locator('.workflows-run-item--selected').innerText()).includes('Cancelled'), 'cancelled state accepted');
    result.checks.push('Web exact human response draft and cancellation');
    await workflowMode('ProjectInput');
    await page.getByTestId('workflows-refresh').click();
    await until(async () => !(await page.getByTestId('workflows-run-test').isDisabled()), 'test admission');
    await page.getByTestId('workflows-run-test').click();
    await page.getByTestId('workflows-preview-input-dialog').waitFor();
    await page.getByTestId('workflows-preview-project-select').selectOption(project);
    const beforeTest = (await workflowState()).tests;
    await page.getByTestId('workflows-preview-input-run').click();
    await until(async () => (await workflowState()).tests === beforeTest + 1, 'synthetic project-input test');
    assert((await workflowState()).lastTestInput.includes(project));
    await page.getByTestId('workflows-run-detail-dialog').waitFor();
    await closeWorkflowDialog('workflows-run-detail-dialog');
    result.checks.push('Web preview input, simulation options and synthetic test execution');
    await workflowMode('HoldTest');
    await page.getByTestId('workflows-refresh').click();
    await until(async () => !(await page.getByTestId('workflows-run-test').isDisabled()), 'held test admission');
    await page.getByTestId('workflows-run-test').click();
    await until(async () => (await workflowState()).tests === beforeTest + 2, 'held test');
    await navigate(route(b), true);
    await until(async () => (await page.getByTestId('workflows-run-item').innerText()).includes('Browser run B'), 'B history');
    await releaseWorkflows();
    await workflowMode('Normal');
    await pause(300);
    assert(!(await page.getByTestId('workflows-run-detail-dialog').isVisible()));
    result.checks.push('Web detached test cannot overwrite the new workflow');
    await workflowTab('analytics');
    await page.getByTestId('workflow-analytics-content').waitFor();
    await page.getByTestId('workflow-analytics-scope').selectOption('SelectedWorkflow');
    await page.getByTestId('workflow-analytics-workflow').selectOption(b);
    await page.getByTestId('workflow-analytics-refresh').click();
    await until(async () => (await workflowState()).analyticsReads >= 3, 'analytics scope');
    await inspect('Web workflow analytics scope and refresh', page.getByTestId('workflow-analytics-content'));
    for (const dismiss of await page.getByRole('button', { name: 'Dismiss notification', exact: true }).all()) {
        if (await dismiss.isVisible()) await dismiss.click();
    }
    await page.screenshot({ path: path.join(raw, 'workflows.png'), fullPage: false });
    await workflowMode('Failed');
    await navigate(route(a), true);
    await page.getByTestId('workflows-error').waitFor();
    assert(!(await page.locator('body').innerText()).includes('PRIVATE_WORKFLOW_PROVIDER_PAYLOAD'));
    await workflowMode('Normal');
    await navigate('/agents/workflows?workflowId=ffffffff-ffff-ffff-ffff-ffffffffffff', true);
    await page.getByTestId('workflows-error').waitFor();
    await workflowTab('workflows');
    assert((await page.getByTestId('workflows-detail').innerText()).includes('No workflow selected'));
    result.checks.push('Web safe failures and explicit missing workflow');
    result.counters = await workflowState();
}
async function workflowSandbox() {
    await navigate('/agents?specimen=workflows');
    const frame = page.getByTestId('sandbox-workflows-specimen');
    const selector = page.locator('#workflow-scenario');
    const scenarios = await selector.locator('option').evaluateAll(items => items.map(item => item.value));
    assert.equal(scenarios.length, 12);
    for (const scenario of scenarios) {
        await selector.selectOption(scenario);
        await until(async () => await frame.getAttribute('data-scenario') === scenario, scenario);
        await inspect(mode + ' Workflows ' + scenario, frame);
        if (scenario === 'Templates') await closeWorkflowDialog('workflows-template-catalogue-dialog');
    }
    await workflowTab('analytics');
    await page.getByTestId('workflow-analytics-content').waitFor();
    await page.getByTestId('workflow-analytics-refresh').click();
    await until(async () => (await page.getByTestId('sandbox-intent').innerText()).includes('Refresh'), 'controlled workflow query intent');
    const specimens = ['catalog', 'capabilities', 'overview', 'governance', 'diagnostics', 'history', 'simple-chat-definitions',
        'simple-chat-definition-editor', 'simple-chat-conversation', 'voice-settings', 'floating-chat-settings', 'agent-chat'];
    for (const specimen of specimens) {
        await navigate('/agents?specimen=' + specimen);
        assert((await page.locator('body').innerText()).trim().length > 100, 'existing specimen visible: ' + specimen);
        result.checks.push(mode + ' existing specimen ' + specimen);
    }
}

async function sandbox() {
    await navigate('/agents?specimen=agent-chat');
    const frame = page.getByTestId('sandbox-agent-chat-specimen');
    const selector = page.locator('#agent-chat-scenario');
    const scenarios = await selector.locator('option').evaluateAll(items => items.map(item => item.value));
    assert.equal(scenarios.length, 12);
    for (const scenario of scenarios) {
        await selector.selectOption(scenario);
        await until(async () => await frame.getAttribute('data-scenario') === scenario, scenario);
        await inspect(mode + ' Agent Chat ' + scenario, frame);
        if (scenario === 'Threads') await inspectChatRail(frame);
    }
    await selector.selectOption('Transcript');
    await send('Controlled sandbox send');
    await until(async () => (await page.getByTestId('sandbox-intent').innerText()).includes('Send'), 'controlled Send');
}
(async () => {
    const log = fs.createWriteStream(path.join(raw, 'browser-' + surfaceName + '-' + mode + '.log'));
    const args = mode === 'web' ? [path.join(root, 'tests/Playwright/GovernanceBrowserFixture/bin/Release/net10.0/GovernanceBrowserFixture.dll'), 'serve-final', root]
        : [path.join(root, 'src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/bin', mode, 'Release/net10.0/CanDoItAll.AgentFramework.UiSandbox.dll')];
    child = spawn('dotnet', args, { cwd: mode === 'web' ? root : path.join(root, 'src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox'),
        env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', DOTNET_ENVIRONMENT: 'Development', ASPNETCORE_URLS: base }, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
    child.stdout.pipe(log);
    child.stderr.pipe(log);
    await until(async () => (await fetch(base + '/agents')).ok, 'host ready', 120000);
    browser = await chromium.launch({ headless: true });
    page = await browser.newPage({ viewport: result.viewport });
    page.setDefaultTimeout(15000);
    page.on('pageerror', error => result.errors.push(error.message));
    page.on('websocket', socket => {
        if (new URL(socket.url()).pathname.endsWith('/_blazor')) socket.on('framereceived', () => frames++);
    });
    await (workflowScope ? mode === 'web' ? workflowWeb() : workflowSandbox() : mode === 'web' ? web() : sandbox());
    assert.deepEqual(result.errors, []);
})().catch(async error => {
    result.failure = error.stack;
    result.failedText = await page?.locator('body').innerText({ timeout: 1000 }).then(text => text.slice(-6000)).catch(() => 'Unavailable');
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
    fs.writeFileSync(path.join(raw, 'browser-' + surfaceName + '-' + mode + '.json'), JSON.stringify(result, null, 2));
    console.log(JSON.stringify(result));
});
