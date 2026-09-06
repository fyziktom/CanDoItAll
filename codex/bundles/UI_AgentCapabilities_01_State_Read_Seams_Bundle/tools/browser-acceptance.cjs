const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require(process.env.CAPA_PLAYWRIGHT);
const out = process.argv[2];
const base = 'http://127.0.0.1:17301';
const steps = [];
async function canonical() { return (await fetch(`${base}/fixture/state`)).json(); }
async function poll(predicate, label) {
 const deadline = Date.now()+20000;
 while (Date.now()<deadline) {
  if (await predicate()) return;
  await new Promise(resolve => setTimeout(resolve,100));
 }
 throw new Error(`Timed out: ${label}`);
}
(async () => {
 const browser = await chromium.launch({headless:true});
 const page = await browser.newPage({viewport:{width:1600,height:1000}});
 const errors=[];
 page.on('pageerror', error => errors.push(error.message));
 const state = await canonical();
 const test = id => page.getByTestId(`agents-capability-${id}`);
 const heading = page.locator('.agent-capabilities-panel__heading');
 const shot = async name => {
  await page.screenshot({path:path.join(out,`${name}.jpeg`),type:'jpeg',quality:75});
  fs.writeFileSync(path.join(out,`${name}-dom.txt`),await page.locator('body').innerText());
 };
 try {
  await page.goto(`http://127.0.0.1:5273/agents?tab=capabilities&agentId=${state.alphaId}`);
  await page.getByRole('button',{name:'Continue',exact:true}).waitFor({timeout:60000});
  await page.getByRole('button',{name:'Continue',exact:true}).click();
  await page.getByRole('button',{name:'Continue',exact:true}).waitFor({state:'hidden'});
  await test('search').waitFor({timeout:60000});
  await poll(async()=> (await heading.innerText()).includes('CAPA01 Alpha'),'selected Alpha');
  await test('search').fill('CAPA01 Inline inspection');
  await poll(async()=> await test('card').count()===1,'filtered catalog');
  await test('type-filter').selectOption('Skill');
  assert.equal(await test('card').count(),1);
  assert.equal(await test('curator-open').isEnabled(),state.curatorAvailable);
  steps.push('Exact requested selection, local search/type filters and managed curator availability');
  await shot('normal');
  if (!(await canonical()).assigned) await test('toggle').click();
  await poll(async()=> (await canonical()).assigned,'canonical assignment');
  await poll(async()=> await test('verify').isEnabled(),'attached verification enabled');
  assert.equal(await test('search').inputValue(),'CAPA01 Inline inspection');
  await test('verify').click();
  await poll(async()=> (await canonical()).proof==='Verified','canonical inline skill verification');
  await poll(async()=> !(await test('verify').isDisabled()),'verification refresh complete');
  await test('access-preview').click();
  await page.getByText('1 allowed',{exact:true}).waitFor();
  steps.push('Assignment and verification through production services; authoritative proof; preserved filter; access preview');
  await shot('preview');
  await test('details').click();
  await page.getByRole('dialog').waitFor();
  await shot('details');
  await page.keyboard.press('Escape');
  await page.getByRole('dialog').waitFor({state:'hidden'});
  for (const kind of ['tool','mcp','skill']) {
   await test(`new-${kind}`).click();
   await page.getByRole('dialog').waitFor();
   await shot(`setup-${kind}`);
   await page.keyboard.press('Escape');
   await page.getByRole('dialog').waitFor({state:'hidden'});
  }
  steps.push('Details and all three real setup dialogs opened and closed');
  await test('toggle').click();
  await poll(async()=> !(await canonical()).assigned,'canonical removal');
  await test('tree-agent').filter({hasText:'CAPA01 Beta'}).click();
  await poll(async()=> (await heading.innerText()).includes('CAPA01 Beta'),'Beta selection');
  await fetch(`${base}/fixture/arm`,{method:'POST'});
  await test('tree-agent').filter({hasText:'CAPA01 Alpha'}).click();
  await poll(async()=> (await canonical()).held,'held Alpha read');
  await test('loading').waitFor();
  assert.equal(await test('toggle').count(),0);
  await shot('loading');
  await test('tree-agent').filter({hasText:'CAPA01 Beta'}).click();
  await poll(async()=> (await heading.innerText()).includes('CAPA01 Beta'),'new Beta replaces pending Alpha');
  assert.equal((await canonical()).cancelled,true);
  await fetch(`${base}/fixture/release`,{method:'POST'});
  await page.waitForTimeout(350);
  assert.match(await heading.innerText(),/CAPA01 Beta/);
  assert.equal(await test('load-failed').count(),0);
  assert.equal(await test('search').inputValue(),'CAPA01 Inline inspection');
  steps.push('Pending A clears prior editor; B supersedes A, cancels owner token, suppresses late A and preserves filters');
  await shot('late-read');
  await page.goto('http://127.0.0.1:5273/agents?tab=capabilities&agentId=00000000-0000-4000-8000-000000000001');
  await test('load-failed').waitFor();
  assert.equal(await test('toggle').count(),0);
  await test('load-retry').click();
  await test('load-failed').waitFor();
  await shot('failed');
  steps.push('Missing requested identity fails closed and Retry keeps the missing target');
  assert.deepEqual(errors,[]);
  fs.writeFileSync(path.join(out,'browser-summary.json'),JSON.stringify({status:'PASS',viewport:{width:1600,height:1000},steps,state,finalCanonical:await canonical(),browserVersion:browser.version(),pageErrors:errors},null,2));
 } catch(error) {
  await shot('failure');
  fs.writeFileSync(path.join(out,'browser-failure.json'),JSON.stringify({steps,error:error.message,pageErrors:errors},null,2));
  throw error;
 } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode=1; });
