
const unpack=r=>r.structuredContent??JSON.parse(r.content.find(x=>x.type==="text").text);
const pw=r=>{const t=r.content?.find(x=>x.type==="text")?.text;if(!t?.includes("### Result\n")){store("cat-last-browser-error",r);throw new Error("Browser tool did not return a result: "+JSON.stringify(r));}return JSON.parse(t.split("### Result\n")[1].split("\n")[0]);};
const helper=load("cat-browser-helper");
const sessionId=load("cat-session");
for (const edit of edits) {
 for (let repeat=1;repeat<=3;repeat++){
 const before=unpack(await tools.mcp__candoitall_dotnetwatch__candoitall_app_status({sessionId})).data;
 if(before.watch?.pendingChange||before.health?.status!=="Healthy")throw new Error("Watch is not ready before sample");
 const trialId=host+"-"+edit.id+"-"+repeat;
 const prepared=pw(await tools.mcp__playwright__browser_run_code_unsafe({code:`async(page)=>{${helper} const edit=${JSON.stringify(edit)}; await fixture(page,edit,'${url}'); await predicate(page,edit,'before',10000); return await coordinator(page,'/trial/start',{host:'${host}',editId:edit.id,trialId:'${trialId}',repeat:${repeat},ready:${JSON.stringify({revision:before.revision,health:before.health,watch:before.watch,lastCursor:before.lastCursor})},manifestId:'CDA-UI-SEAMS-CATALOG-01-SB00',navigationBefore:await page.evaluate(()=>({timeOrigin:performance.timeOrigin,type:performance.getEntriesByType('navigation')[0]?.type}))});}`}));
 let settled;
 for(let i=0;i<4;i++){
 settled=unpack(await tools.mcp__candoitall_dotnetwatch__candoitall_app_wait({sessionId,condition:"WatchSettled",cursor:before.lastCursor,timeoutMs:30000,pollIntervalMs:500,quietPeriodMs:750}));
 if(settled.data?.satisfied||["Failed","ExitedUnexpectedly"].includes(settled.data?.observedState))break;
 notify({trialId,waiting:settled.data?.watch?.state});
 }
 const after=unpack(await tools.mcp__candoitall_dotnetwatch__candoitall_app_status({sessionId})).data;
 const observed=pw(await tools.mcp__playwright__browser_run_code_unsafe({code:`async(page)=>{${helper} const edit=${JSON.stringify(edit)};let browserReload=false,error,value;try{try{value=await predicate(page,edit,'after',2000);}catch{browserReload=true;await fixture(page,edit,'${url}',true);value=await predicate(page,edit,'after',30000);}}catch(e){error=e.message;}return await coordinator(page,'/observed',{outcome:error?'failure':'success',error,browserReload,navigationAfter:await page.evaluate(()=>({timeOrigin:performance.timeOrigin,type:performance.getEntriesByType('navigation')[0]?.type})),browserAssertion:{selector:edit.selector,expected:edit.after,actual:value?.slice(0,250)},watch:${JSON.stringify(after)},settled:${JSON.stringify(settled.data)},mechanism:${JSON.stringify(before.health.runtimePid!==after.health?.runtimePid)}?'process restart':browserReload?'browser reload':'hot reload'});}`}));
 notify({trialId,outcome:observed.outcome,elapsedMs:observed.elapsedMs,mechanism:observed.mechanism});
 const undoCursor=after.lastCursor;
 await tools.mcp__playwright__browser_run_code_unsafe({code:`async(page)=>{${helper} return await coordinator(page,'/undo',{});}`});
 let undo;
 for(let i=0;i<4;i++){
 undo=unpack(await tools.mcp__candoitall_dotnetwatch__candoitall_app_wait({sessionId,condition:"WatchSettled",cursor:undoCursor,timeoutMs:30000,pollIntervalMs:500,quietPeriodMs:750}));
 if(undo.data?.satisfied)break;
 notify({trialId,undoWaiting:undo.data?.watch?.state});
 }
 const restored=pw(await tools.mcp__playwright__browser_run_code_unsafe({code:`async(page)=>{${helper}const edit=${JSON.stringify(edit)};let confirmed=false,error,browserReload=false;try{try{await predicate(page,edit,'before',2000);}catch{browserReload=true;await fixture(page,edit,'${url}',true);await predicate(page,edit,'before',30000);}confirmed=true;}catch(e){error=e.message;}return await coordinator(page,'/undo-observed',{confirmed,error,browserReload,watch:${JSON.stringify(undo.data)}});}`}));
 if(!restored.undoConfirmed)throw new Error("Visible undo failed for "+trialId);
 if(observed.outcome!=="success")throw new Error("Primary failure retained; inspect before next edit "+trialId);
 }
}
