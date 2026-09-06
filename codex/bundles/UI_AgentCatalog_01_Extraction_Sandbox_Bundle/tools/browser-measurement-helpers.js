
async function fixture(page, edit, url, reload=false) {
  if (reload || page.url().split('?')[0]!==url.split('?')[0]) await page.goto(url,{waitUntil:'domcontentloaded',timeout:30000});
  await page.locator('[data-testid=agents-catalog-search]').waitFor({state:'visible',timeout:30000});
  const consent=page.getByRole('button',{name:'Continue',exact:true});
  if(await consent.isVisible().catch(()=>false)) await consent.click();
  const search=page.locator('[data-testid=agents-catalog-search] input');
  const actual=await search.count()?search:page.locator('[data-testid=agents-catalog-search]');
  await actual.fill(edit.fixture==='empty'?'catalog-no-matches-92b7':'');
  if(edit.fixture==='empty') await page.waitForFunction(()=>document.querySelectorAll('[data-testid=agents-catalog-card-grid] [data-testid^=agent-selection-card-]').length===0);
  if(edit.fixture==='theme-probe') await page.evaluate(()=>{
    document.querySelector('[data-testid=catalog-theme-probe]')?.remove();
    const probe=document.createElement('div');
    probe.dataset.testid='catalog-theme-probe';probe.className='cda-admin-kicker';
    probe.textContent='Catalog theme measurement';
    probe.style.cssText='position:fixed;bottom:4px;left:8px;z-index:99999;font-size:12px;';
    document.body.append(probe);
  });
  await page.evaluate(async()=>{window.scrollTo(0,0);document.querySelectorAll('.agent-catalog-panel__agent-scroll, .agent-catalog-panel__team-panel').forEach(e=>e.scrollTop=0);await document.fonts.ready;await new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)));});
}
async function predicate(page,edit,phase,timeout=30000){
  const expected=edit[phase];
  await page.waitForFunction(({selector,property,expected,forbidden})=>{
    const nodes=[...document.querySelectorAll(selector)];
    return property?nodes.some(n=>Math.abs(parseFloat(getComputedStyle(n)[property])-expected)<0.04):nodes.some(n=>n.textContent.includes(expected) && (!forbidden || !n.textContent.includes(forbidden)));
  },{selector:edit.selector,property:edit.property,expected,forbidden:phase==='before'?edit.after:undefined},{timeout,polling:100});
  await page.evaluate(()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r))));
  return await page.locator(edit.selector).first().evaluate((e,p)=>p?getComputedStyle(e)[p]:e.textContent,edit.property||null);
}
async function coordinator(page,path,data){
 const response=await page.request.post('http://127.0.0.1:17299'+path,{data,timeout:10000});
 const result=await response.json(); if(!response.ok())throw new Error(JSON.stringify(result)); return result;
}
