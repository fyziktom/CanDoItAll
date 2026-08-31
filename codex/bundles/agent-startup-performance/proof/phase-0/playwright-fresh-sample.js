async (page) => {
 const p=page.context().browser().contexts().flatMap(c=>c.pages()).find(p=>p.url().startsWith('http://localhost:PORT/'));
 const chat=p.getByTestId('floating-agent-chat-window');
 if(await chat.count() && await chat.getByTestId('conversation-message').count()>0){
  await chat.getByTestId('agents-chat-focused-new-thread').click();
  await chat.waitFor({state:'detached',timeout:15000});
 }
 if(await chat.count()===0)await p.getByRole('button',{name:'AGENTBUTTON',exact:true}).click();
 await chat.getByText('This thread is ready for the first prompt',{exact:true}).waitFor({timeout:15000});
 if(await chat.getByTestId('conversation-message').count()!==0)throw new Error('Fresh session required');
 await chat.getByTestId('chat-prompt-input').fill('Explain why decimal is preferable to binary floating point for currency calculations, in two sentences. Do not invoke tools.');
 await p.evaluate(({id,port})=>{
  window.__startupProbe?.observer?.disconnect();
  const root=document.querySelector('[data-testid="floating-agent-chat-window"]');
  const sample={id,port,submitUtc:new Date().toISOString(),submitMono:performance.now(),firstContentUtc:null,firstContentMono:null,terminalUiUtc:null,terminalUiMono:null,stages:[]};
  const check=()=>{const content=[...root.querySelectorAll('[data-testid="conversation-message"].justify-start .chat-markdown')].find(e=>e.textContent.trim());
   if(content&&!sample.firstContentUtc){sample.firstContentUtc=new Date().toISOString();sample.firstContentMono=performance.now();}
   const send=root.querySelector('[data-testid="chat-send-button"]');
   const done=root.querySelector('[data-testid="chat-execution-summary"]');
   if(sample.firstContentUtc&&done&&send&&!send.disabled&&!sample.terminalUiUtc){sample.terminalUiUtc=new Date().toISOString();sample.terminalUiMono=performance.now();}
   for(const e of root.querySelectorAll('.agent-execution-stream__phase')){const stage=e.textContent.trim();if(stage&&!sample.stages.some(x=>x.stage===stage))sample.stages.push({stage,utc:new Date().toISOString(),mono:performance.now()});}
  };
  const observer=new MutationObserver(check);observer.observe(root,{subtree:true,childList:true,characterData:true,attributes:true,attributeFilter:['disabled','data-state']});
  window.__startupProbe={sample,observer};
 },{id:'SAMPLEID',port:PORT});
 await chat.getByTestId('chat-send-button').click();
 try{await p.waitForFunction(()=>window.__startupProbe.sample.terminalUiUtc,{},{timeout:40000});}catch{}
 return {sample:await p.evaluate(()=>window.__startupProbe.sample),text:(await chat.innerText()).slice(-2000)};
}