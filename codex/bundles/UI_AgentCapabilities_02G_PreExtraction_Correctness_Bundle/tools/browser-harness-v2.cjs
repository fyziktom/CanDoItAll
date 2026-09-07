const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require(process.env.CAPA_PLAYWRIGHT);
const out = process.argv[2];
fs.mkdirSync(out, { recursive:true });
const control = 'http://127.0.0.1:17301';
const steps = [];
const canonical = async () => (await fetch(control+'/fixture/state')).json();
const call = async name => assert.equal((await fetch(control+'/fixture/'+name, {method:'POST'})).ok,true);
async function poll(predicate,label) {
 const end=Date.now()+60000;
 while(Date.now()<end) {
  if(await predicate()) return;
  await new Promise(resolve=>setTimeout(resolve,100));
 }
 throw new Error('Timed out: '+label);
}
(async()=>{
 const browser=await chromium.launch({headless:true});
 const page=await browser.newPage({viewport:{width:1600,height:1000}});
 const errors=[];
 page.on('pageerror',error=>errors.push(error.message));
 const test=id=>page.getByTestId('agents-capability-'+id);
 const shot=async name=>{
  await page.screenshot({path:path.join(out,name+'.jpeg'),type:'jpeg',quality:80});
  fs.writeFileSync(path.join(out,name+'-dom.txt'),await page.locator('body').innerText());
 };
 const initial=await canonical();
 try {
  await page.goto('http://127.0.0.1:5273/agents?tab=capabilities&agentId='+initial.alphaId);
  const proceed=page.getByRole('button',{name:'Continue',exact:true});
  await proceed.waitFor({timeout:90000});
  await proceed.click();
  await proceed.waitFor({state:'hidden'});
  await test('search').waitFor({timeout:90000});
  await test('search').fill('CAPA02G Inline inspection');
  await test('access-reason').fill('  Keep this raw diagnostic reason  ');
  await shot('normal');
  if(!(await canonical()).assigned) {
   await test('toggle').click();
   await poll(async()=> (await canonical()).assigned,'authoritative assignment');
  }
  await poll(async()=> !(await test('verify').isDisabled()),'diagnostic enabled');
  const beforeDiagnostic=await canonical();
  await call('unknown-diagnostic');
  await test('verify').click();
  await test('acknowledge-diagnostic').waitFor();
  assert.equal(await test('verify').isDisabled(),true);
  assert.match(await test('operation').innerText(),/may have executed/);
  const unknownDiagnostic=await canonical();
  assert.equal(unknownDiagnostic.diagnosticCalls,beforeDiagnostic.diagnosticCalls+1);
  assert.equal(unknownDiagnostic.proof,'Verified');
  await shot('receiptless-diagnostic');
  await test('acknowledge-diagnostic').click();
  await test('acknowledge-diagnostic').waitFor({state:'hidden'});
  assert.equal(await test('verify').isDisabled(),false);
  assert.equal((await canonical()).diagnosticCalls,unknownDiagnostic.diagnosticCalls);
  assert.equal(await test('access-reason').inputValue(),'  Keep this raw diagnostic reason  ');
  steps.push('Real diagnostic publishes proof before a lost response; explicit acknowledgement unlocks without replay and preserves raw draft.');
  await test('verify').click();
  await poll(async()=> (await canonical()).diagnosticCalls===unknownDiagnostic.diagnosticCalls+1,'new explicit diagnostic');
  await poll(async()=> !(await test('verify').isDisabled()),'new diagnostic reconciled');
  await test('details').click();
  await page.getByRole('dialog').waitFor();
  await shot('details');
  await page.keyboard.press('Escape');
  await page.getByRole('dialog').waitFor({state:'hidden'});
  assert.equal(initial.curatorAvailable,true);
  const beforeCurator=await canonical();
  await call('unknown-curator');
  await test('curator-open').click();
  await test('curator-acknowledge').waitFor({timeout:90000});
  await page.getByTestId('floating-agent-chat-content').waitFor();
  assert.match(await page.getByTestId('floating-agent-chat-content').innerText(),/CONVERSATION|CONTEXT|Ready|ready/);
  const unknownCurator=await canonical();
  assert.ok(unknownCurator.chatSessionId);
  assert.equal(unknownCurator.curatorCalls,beforeCurator.curatorCalls+1);
  assert.equal(unknownCurator.curatorSessionCount,beforeCurator.curatorSessionCount+1);
  const header=await page.locator('.agent-capabilities-panel__detail-header').boundingBox();
  const stats=await page.locator('.agent-capabilities-panel__header-stats').boundingBox();
  const warning=await test('curator-unconfirmed').boundingBox();
  assert.ok(stats.width>=200,'Header statistics retain readable width');
  assert.ok(warning.y>=header.y+header.height,'Recovery warning occupies its own detail row');
  await shot('curator-unknown-managed-chat');
  await test('curator-acknowledge').click();
  await test('curator-acknowledge').waitFor({state:'hidden'});
  const acknowledged=await canonical();
  assert.equal(acknowledged.curatorCalls,unknownCurator.curatorCalls);
  assert.equal(acknowledged.curatorSessionCount,unknownCurator.curatorSessionCount);
  assert.equal(acknowledged.chatSessionId,unknownCurator.chatSessionId);
  assert.equal(await test('curator-open').isDisabled(),false);
  await shot('curator-acknowledged');
  steps.push('Inspected the actual visible managed chat after lost creation response; acknowledgement preserves its canonical identity and performs no launch or deletion.');
  assert.deepEqual(errors,[]);
  fs.writeFileSync(path.join(out,'browser-summary.json'),JSON.stringify({status:'PASS',viewport:{width:1600,height:1000},browserVersion:browser.version(),steps,initial,final:await canonical(),pageErrors:errors},null,2));
 } catch(error) {
  await shot('failure');
  fs.writeFileSync(path.join(out,'browser-failure.json'),JSON.stringify({steps,error:error.message,pageErrors:errors},null,2));
  throw error;
 } finally {
  await browser.close();
 }
})().catch(error=>{console.error(error);process.exitCode=1;});
