
const unpack=r=>r.structuredContent??JSON.parse(r.content.find(x=>x.type==="text").text);
const pw=r=>{const t=r.content?.find(x=>x.type==="text")?.text;if(!t?.includes("### Result\n")){store("cat-last-browser-error",r);throw new Error("Browser tool did not return a result: "+JSON.stringify(r));}return JSON.parse(t.split("### Result\n")[1].split("\n")[0]);};
const helper=load("cat-browser-helper");
for(const repeat of repeats){
 await tools.mcp__playwright__browser_run_code_unsafe({code:"async(page)=>{await page.goto('about:blank');return {blank:true};}"});
 const previous=load("cat-session");
 if(previous){const stopped=unpack(await tools.mcp__candoitall_dotnetwatch__candoitall_app_stop({sessionId:previous}));store(host+"-stop-before-cold-"+repeat,stopped);}
 await tools.mcp__playwright__browser_run_code_unsafe({code:`async(page)=>{${helper}let alive=false;try{alive=(await page.request.get('${url.split('/agents')[0]}/_dev/runtime',{timeout:2000})).ok();}catch{}if(alive)throw new Error('Previous runtime still owns the measurement port');return await coordinator(page,'/cold/start',{host:'${host}',trialId:'${host}-cold-${repeat}',cache:'restored and previously compiled; process-cold',launchDispatchIncluded:true});}`});
 const start=unpack(await tools.mcp__candoitall_dotnetwatch__candoitall_app_start(request));
 store(host+"-cold-"+repeat+"-start",start);
 if(!start.ok)throw new Error("Cold launch failed");
 const sessionId=start.data.sessionId;store("cat-session",sessionId);
 let ready;
 for(let i=0;i<12;i++){
   ready=unpack(await tools.mcp__candoitall_dotnetwatch__candoitall_app_wait({sessionId,condition:"Ready",timeoutMs:30000,pollIntervalMs:500}));
   if(ready.data?.satisfied)break;
   notify({cold:host+"-"+repeat,state:ready.data?.observedState,watch:ready.data?.watch?.state});
   if(["Failed","ExitedUnexpectedly"].includes(ready.data?.observedState))break;
 }
 const result=pw(await tools.mcp__playwright__browser_run_code_unsafe({code:`async(page)=>{${helper}let assertion,error;try{
  await page.goto('${url}',{waitUntil:'domcontentloaded',timeout:30000});
  await page.locator('[data-testid=agents-catalog-search]').waitFor({state:'visible',timeout:45000});
  const consent=page.getByRole('button',{name:'Continue',exact:true}),input=page.locator('[data-testid=agents-catalog-search]');
  for(let i=0;i<12;i++){
    if(await consent.isVisible().catch(()=>false))await consent.click();
    await input.fill('');await input.fill('catalog-fixture');
    try{await page.waitForFunction(()=>document.querySelector('[data-testid=agents-catalog-card-grid]')?.children.length===12,null,{timeout:2000});break;}catch(e){if(i===11)throw e;}
  }
  await input.fill('');await page.waitForFunction(()=>document.querySelector('[data-testid=agents-catalog-card-grid]')?.children.length===40,null,{timeout:10000});
  await page.evaluate(async()=>{await document.fonts.ready;await Promise.all([...document.images].map(i=>i.decode().catch(()=>{})));await new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)));});
  assertion={cards:40,interactiveSearchCount:12,viewport:page.viewportSize()};
 }catch(e){error=e.message;}
 return await coordinator(page,'/observed',{outcome:error?'failure':'success',error,mechanism:'process-cold launch',browserAssertion:assertion,managedReady:${JSON.stringify(ready)},launch:${JSON.stringify(start)}});}`}));
 notify({cold:result.trialId,outcome:result.outcome,elapsedMs:result.elapsedMs});
 if(result.outcome!=="success")throw new Error("Cold sample failed; retained for diagnosis");
}
