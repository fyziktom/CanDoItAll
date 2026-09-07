from pathlib import Path
import json,subprocess,datetime,hashlib,os,sys,re
root=Path.cwd();out=Path('.mcp-state/overview03/final/hygiene');out.mkdir(exist_ok=True)
summary=json.loads(Path('.mcp-state/overview03/final/stable-summary.json').read_text())
verification=json.loads(Path('.mcp-state/overview03/final/stable-discovery-verification.json').read_text())
assert summary['exit']==0 and verification['pass'] and verification['allMethodIdentitiesAccounted']
assert summary['executedCases']==verification['executedCases'] and summary['discoveredRows']==verification['discoveredEntries']
assert all(not x['nonPass'] for x in summary['results'])
p=root/'src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor.css'
before=p.read_bytes();assert len(before)==643 and before.endswith(b'}\n}\n\n')
after=before[:-1];assert len(after)==642 and before.splitlines()==after.splitlines()+[b'']
sha=lambda data:hashlib.sha256(data).hexdigest()
assets={q:q.read_bytes() for q in [root/'src/App/CanDoItAll.Web/wwwroot/css/output.css',root/'src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/wwwroot/css/catalog-fast.css']}
module=root/'src/Modules/CanDoItAll.Modules.AgentFramework'
generated={q:q.read_bytes() for q in (module/'obj/Release').rglob('*AgentsHomePage*.css')}
assert generated,'The compiled owning CSS must exist for comparison.'
(out/'AgentsHomePage.before.css').write_bytes(before);p.write_bytes(after)
receipt={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'source':p.relative_to(root).as_posix(),'beforeSha256':sha(before),'afterSha256':sha(after),'removedBytes':1,'onlyRemovedFinalBlankLine':True,'reason':'git diff --check found one newly added extra EOF blank line; no CSS selector/value or measured probe changes. Cleanup deliberately waits until broad stable completion.','commands':[]}
def run(args,name):
 start=datetime.datetime.now(datetime.timezone.utc).isoformat()
 with (out/name).open('w',encoding='utf-8') as log:r=subprocess.run(args,stdout=log,stderr=subprocess.STDOUT)
 receipt['commands'].append({'command':args,'exit':r.returncode,'startedUtc':start,'finishedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'output':name})
 (out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf-8')
 print(name,r.returncode,flush=True)
 assert r.returncode==0,name
for label,project in [('module','src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj'),('web','src/App/CanDoItAll.Web/CanDoItAll.Web.csproj'),('browser-fixture','.mcp-state/overview-browser-fixture/OverviewBrowser.csproj')]:
 run(['dotnet','build',project,'-c','Release','--nologo','/m:1','/p:UseSharedCompilation=false','-v:q'],'build-'+label+'.txt')
receipt['generatedCss']=[]
for q,data in generated.items():
 current=q.read_bytes();onlyBlankLines=[x for x in data.splitlines() if x.strip()]==[x for x in current.splitlines() if x.strip()]
 assert onlyBlankLines,q
 receipt['generatedCss'].append({'path':q.relative_to(root).as_posix(),'beforeSha256':sha(data),'afterSha256':sha(current),'allNonblankLinesIdentical':True})
receipt['assets']=[{'path':q.relative_to(root).as_posix(),'sha256':sha(q.read_bytes()),'unchanged':q.read_bytes()==data} for q,data in assets.items()]
assert all(x['unchanged'] for x in receipt['assets'])
run(['git','diff','--check'],'diff-check.txt')
script=Path('.mcp-state/overview03/browser-web.cjs').read_text().replace(".mcp-state/overview03/browser-web-r2",".mcp-state/overview03/browser-web-hygiene")
scriptPath=out/'browser-web-hygiene.cjs';scriptPath.write_text(script,encoding='utf-8',newline='\n')
os.environ['OV_PLAYWRIGHT']=r'C:\Users\lucys\AppData\Local\npm-cache\_npx\e41f203b7505f1fb\node_modules\playwright'
run(['node',str(scriptPath)],'browser-web-hygiene.txt')
receipt['status']='PASS; final source hygiene is blank-line-only, owning builds/browser rechecked. Measured probe/Tailwind bytes are unchanged; no timing or broad semantic invalidation.'
(out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf-8')
print(receipt['status'],flush=True)
