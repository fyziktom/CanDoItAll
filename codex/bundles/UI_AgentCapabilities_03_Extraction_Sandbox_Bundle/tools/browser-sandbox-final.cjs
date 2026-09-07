const { chromium } = require(process.env.CAPA_PLAYWRIGHT);
const fs = require('node:fs');
const path = require('node:path');
const { spawn, execFileSync } = require('node:child_process');
const assert = require('node:assert/strict');
const root = process.cwd();
const project = path.join(root, 'src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox');
const output = path.resolve(process.argv[2] || '.mcp-state/capa03/browser-SB02');
const scenarios = [
'baseline','loading','failed','missing-target','no-agents','no-capabilities','selected','kinds-and-proof','long-content',
'assignment-pending','assignment-rejected','assignment-conflict','committed-warning','unconfirmed','exact-before','intervening',
'verification-pending','verification-superseded','verification-recovery','diagnostic-acknowledgement',
'preview-busy','preview-valid','preview-invalid','curator-available','curator-unavailable','curator-pending',
'curator-opened','curator-unconfirmed','curator-acknowledged'];
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
async function run(mode, port) {
    const out = path.join(output, mode);
    fs.mkdirSync(out, {recursive:true});
    const base = 'http://127.0.0.1:' + port;
    const log = fs.createWriteStream(path.join(out,'host.txt'));
    const child = spawn('dotnet', [path.join(project,'bin',mode,'Release/net10.0/CanDoItAll.AgentFramework.UiSandbox.dll'),'--urls',base],
        {cwd:project,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',DOTNET_ENVIRONMENT:'Development',CatalogAssetMode:mode},
         windowsHide:true,stdio:['ignore','pipe','pipe']});
    child.stdout.pipe(log);
    child.stderr.pipe(log);
    let browser;
    const results = {mode,port,hostPid:child.pid,viewport:{width:1600,height:1000},scenarios:[],screenshots:[],errors:[],status:'RUNNING'};
    async function save(page, name) {
        await page.screenshot({path:path.join(out,name+'.jpeg'),type:'jpeg',quality:82});
        fs.writeFileSync(path.join(out,name+'-dom.txt'),await page.locator('body').innerText());
        results.screenshots.push(name);
    }
    try {
        const deadline = Date.now()+60000;
        while (true) {
            try {
                const r = await fetch(base+'/_dev/runtime',{signal:AbortSignal.timeout(1000)});
                if (r.ok) {
                    results.runtime = await r.json();
                    assert.equal(results.runtime.assetMode,mode);
                    break;
                }
            } catch {}
            assert(Date.now()<deadline && child.exitCode===null,'Sandbox did not start.');
            await pause(150);
        }
        browser = await chromium.launch({headless:true});
        const page = await browser.newPage({viewport:results.viewport});
        results.browserVersion=browser.version();
        page.on('pageerror',e=>results.errors.push(e.message));
        await page.goto(base+'/agents?specimen=capabilities&scenario=baseline&layout=matched',{waitUntil:'networkidle'});
        await page.locator('[data-testid="agents-capabilities-panel"]').waitFor();
        await page.waitForTimeout(700);
        const responsive = await page.getByText('129 matching capability(s)', { exact: true }).evaluate(el => {
            const split = el.parentElement.parentElement;
            const style = getComputedStyle(split);
            return { className: split.className, flexDirection: style.flexDirection, alignItems: style.alignItems, justifyContent: style.justifyContent };
        });
        results.responsiveFilter = responsive;
        assert.equal(responsive.flexDirection, 'row', 'Responsive_filter_layout_matches_full_app_at_large_desktop');
        assert.equal(responsive.alignItems, 'center');
        assert.equal(responsive.justifyContent, 'space-between');
        await save(page, 'baseline-responsive');
        const initialHistory = await page.evaluate(()=>history.length);
        for (const scenario of scenarios) {
            if (scenario!=='baseline') {
                await page.locator('[data-testid="sandbox-capabilities-scenario"]').selectOption(scenario);
                await page.waitForURL(u=>u.searchParams.get('scenario')===scenario);
            }
            const state = await page.evaluate(()=>{
                const one = s=>document.querySelector(s);
                const all = s=>[...document.querySelectorAll(s)];
                const style = e=>e ? {display:getComputedStyle(e).display,width:e.getBoundingClientRect().width,
                    x:e.getBoundingClientRect().x,y:e.getBoundingClientRect().y,overflowY:getComputedStyle(e).overflowY,
                    clientHeight:e.clientHeight,scrollHeight:e.scrollHeight,
                    scope:[...e.attributes].filter(a=>a.name.startsWith('b-')).map(a=>a.name)} : null;
                return {
                    heading:one('.agent-capabilities-panel__heading')?.textContent,
                    target:new URL(location.href).searchParams.get('agentId'),
                    loading:!!one('[data-testid="agents-capability-loading"]'),
                    failed:!!one('[data-testid="agents-capability-load-failed"]'),
                    cards:all('[data-testid="agents-capability-card"]').map(e=>({kind:e.dataset.capabilityKind,
                        text:e.innerText,width:e.getBoundingClientRect().width,contentWidth:e.querySelector('.agent-capability-list__content')?.getBoundingClientRect().width,disabled:[...e.querySelectorAll('button')].map(b=>b.disabled)})),
                    curatorDisabled:one('[data-testid="agents-capability-curator-open"]')?.disabled,
                    operation:one('[data-testid="agents-capability-operation"]')?.innerText,
                    curatorUnknown:!!one('[data-testid="agents-capability-curator-unconfirmed"]'),
                    root:style(one('.agent-capabilities-surface')),
                    tree:style(one('.agent-capabilities-panel__tree-scroll')),
                    cardScroll:style(one('.agent-capabilities-panel__card-scroll')),
                    stats:style(one('.agent-capabilities-panel__header-stats')),frame:style(one('main')),horizontalOverflow:document.documentElement.scrollWidth>innerWidth
                };
            });
            assert.equal(state.loading,scenario==='loading');
            assert.equal(state.failed,['failed','missing-target'].includes(scenario));
            assert.equal(state.root.display,'contents');
            assert(state.root.scope.length>0,'Moved CSS scope anchor missing.');
            assert(!state.horizontalOverflow,scenario+' horizontal overflow');
            assert(Math.abs(state.frame.x-125)<1,'Matched left frame differs.');
            assert(Math.abs(state.frame.y-222.59375)<1,'Matched top frame differs from the pre-extraction baseline.');
            if (scenario==='long-content') {
                fs.writeFileSync(path.join(out,'long-geometry.json'),JSON.stringify(state,null,2));
                assert(state.stats.width>=200,'Long heading must not collapse readable header statistics.');
                assert(state.cards.every(c=>c.contentWidth<c.width),'Long card content must remain within its card.');
            }
            if (scenario==='kinds-and-proof') {
                assert.deepEqual(state.cards.map(c=>c.kind).sort(),['McpServer','Skill','Tool','Plugin','Rag','AiContext','Memory'].sort());
                for(const label of ['Verified','Not run','Failed','Pending review']) assert(state.cards.some(c=>c.text.includes(label)));
            }
            if (['assignment-pending','committed-warning','unconfirmed','exact-before','intervening',
                'verification-pending','verification-recovery','diagnostic-acknowledgement'].includes(scenario)) {
                assert(state.cards.every(c=>c.disabled[0] && c.disabled[1]),'Busy assignment and verify must be disabled.');
            }
            if (scenario==='curator-unconfirmed') assert(state.curatorUnknown && state.curatorDisabled);
            results.scenarios.push({scenario,...state});
            if (['kinds-and-proof','long-content','failed','committed-warning','diagnostic-acknowledgement','curator-unconfirmed'].includes(scenario)) {
                await save(page,scenario);
            }
        }
        assert.equal(await page.evaluate(()=>history.length),initialHistory,'Scenario normalization must replace history.');
        await page.locator('[data-testid="sandbox-capabilities-scenario"]').selectOption('selected');
        await page.waitForURL(u=>u.searchParams.get('scenario')==='selected');
        await page.locator('[data-testid="agents-capability-access-reason"]').fill('  Preserve raw sandbox reason  ');
        await page.locator('[data-testid="agents-capability-access-reason"]').press('Tab');
        await page.locator('[data-testid="agents-capability-search"]').fill('Review skill');
        await page.waitForFunction(()=>document.querySelectorAll('[data-testid="agents-capability-card"]').length===1);
        await page.locator('[data-testid="sandbox-capabilities-scenario"]').selectOption('unconfirmed');
        await page.locator('[data-testid="agents-capability-recover"]').waitFor();
        assert.equal(await page.locator('[data-testid="agents-capability-access-reason"]').inputValue(),'  Preserve raw sandbox reason  ');
        assert.equal(await page.locator('[data-testid="agents-capability-search"]').inputValue(),'Review skill');
        await page.locator('[data-testid="agents-capability-recover"]').click();
        await page.waitForFunction(()=>document.querySelector('[data-testid="sandbox-intent"]').textContent.includes('No mutation is replayed'));
        assert.equal(await page.locator('[data-testid="agents-capability-toggle"]').isEnabled(),true);
        await page.getByRole('button',{name:'Reset',exact:true}).click();
        await page.waitForFunction(()=>document.querySelectorAll('[data-testid="agents-capability-card"]').length===7);
        results.rawDraftAndFilters='PASS';
        const tag = page.locator('[data-testid="agents-capability-tag-filter-input"]');
        await tag.fill('review');
        await tag.press('Enter');
        await page.waitForFunction(()=>document.querySelectorAll('[data-testid="agents-capability-card"]').length===2);
        await page.locator('[data-testid="agents-capability-type-filter"]').selectOption('Skill');
        await page.waitForFunction(()=>document.querySelectorAll('[data-testid="agents-capability-card"]').length===1);
        await page.locator('[data-testid="agents-capability-assignment-filter"]').selectOption('Assigned');
        await page.getByText('No capabilities match the current filters',{exact:true}).waitFor();
        await page.getByRole('button',{name:'Reset',exact:true}).click();
        await page.waitForFunction(()=>document.querySelectorAll('[data-testid="agents-capability-card"]').length===7);
        await page.getByRole('button',{name:'Collapse Agents',exact:true}).click();
        await page.waitForFunction(()=>document.querySelector('[data-testid="agents-capability-tree-root"]').getAttribute('aria-expanded')==='false');
        assert.equal(await page.locator('[data-testid="agents-capability-tree-agent"]').count(),0);
        await page.getByRole('button',{name:'Expand Agents',exact:true}).click();
        await page.waitForFunction(()=>document.querySelector('[data-testid="agents-capability-tree-root"]').getAttribute('aria-expanded')==='true');
        const endpoint = page.locator('.agent-capability-list__endpoint').first();
        await endpoint.focus();
        assert.equal(await endpoint.getAttribute('title'),await endpoint.innerText());
        assert.equal(await endpoint.getAttribute('aria-label'),'Path or endpoint: '+await endpoint.innerText());
        results.filtersTreeAndEndpointAccessibility='PASS';


        for (const [scenario,selector,message] of [
            ['diagnostic-acknowledgement','agents-capability-acknowledge-diagnostic','No diagnostic runs.'],
            ['curator-unconfirmed','agents-capability-curator-acknowledge','No chat is created or deleted.'],
            ['exact-before','agents-capability-retry-assignment','Deliberate sample'],
            ['intervening','agents-capability-adopt','Sample current state adopted.']
        ]) {
            await page.locator('[data-testid="sandbox-capabilities-scenario"]').selectOption(scenario);
            await page.locator('[data-testid="'+selector+'"]').click();
            await page.waitForFunction(text=>document.querySelector('[data-testid="sandbox-intent"]').textContent.includes(text),message);
            assert.equal(await page.locator('[data-testid="'+selector+'"]').count(),0);
        }
        results.recoveryActions='PASS';
        await page.locator('[data-testid="sandbox-capabilities-scenario"]').selectOption('kinds-and-proof');
        await page.waitForURL(u=>u.searchParams.get('scenario')==='kinds-and-proof');
        await page.reload({waitUntil:'networkidle'});
        assert.equal(await page.locator('[data-testid="sandbox-capabilities-scenario"]').inputValue(),'kinds-and-proof');
        await page.waitForTimeout(500);
        await page.locator('[data-testid="sandbox-layout"]').click();
        await page.waitForURL(u=>u.searchParams.get('layout')==='flexible');
        await page.locator('[data-testid="sandbox-catalog"]').click();
        await page.locator('[data-testid="sandbox-card-states"]').click();
        await page.waitForURL(u=>u.searchParams.get('scenario')==='card-states');
        assert.equal(new URL(page.url()).searchParams.get('layout'),'flexible');
        await page.reload({waitUntil:'networkidle'});
        assert.equal(await page.locator('.catalog-sandbox__specimen').count(),1);
        results.catalogCompatibility='PASS';
        const theme = mode==='Parity'?'output.css':'catalog-fast.css';
        const response = await page.request.get(base+'/css/'+theme);
        assert(response.ok());
        results.theme={path:theme,status:response.status(),bytes:(await response.body()).length};
        assert.deepEqual(results.errors,[]);
        results.status='PASS';
    } catch (error) {
        results.status='FAIL';
        results.failure={message:error.message,stack:error.stack};
        if(browser) {
            const pages=browser.contexts().flatMap(c=>c.pages());
            if(pages.length) await save(pages[0],'failure').catch(()=>{});
        }
        process.exitCode=1;
    } finally {
        if(browser) await browser.close();
        if(child.pid && child.exitCode===null) {
            execFileSync('taskkill',['/PID',String(child.pid),'/T','/F'],{windowsHide:true,stdio:'ignore'});
        }
        await pause(250);
        results.hostStopped=child.exitCode!==null;
        fs.writeFileSync(path.join(out,'summary.json'),JSON.stringify(results,null,2));
        process.stdout.write(JSON.stringify({mode,status:results.status,scenarios:results.scenarios.length,failure:results.failure?.message})+'\n');
    }
    return results.status==='PASS';
}
(async()=>{
    await run('Parity',5393);
    await run('Fast',5394);
})();

