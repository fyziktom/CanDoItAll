from pathlib import Path
import json,subprocess,datetime,sys
stage=sys.argv[1] if len(sys.argv)>1 else 'before'
root=Path.cwd();out=root/'.mcp-state/overview03';pending=[root/'src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/CanDoItAll.AgentFramework.UiSandbox.csproj'];nodes=[];seen=set()
while pending:
 p=pending.pop(0).resolve()
 if str(p).lower() in seen:continue
 seen.add(str(p).lower())
 raw=subprocess.check_output(['dotnet','msbuild',str(p),'-getItem:ProjectReference','-getProperty:UseLocalCanDoItAllLibraries,TargetFramework,CatalogAssetMode,IsPackable'],text=True,encoding='utf-8')
 data=json.loads(raw[raw.index('{'):]);refs=[Path(x['FullPath']).resolve() for x in data['Items']['ProjectReference']]
 nodes.append({'project':str(p),'references':[str(x) for x in refs],'properties':data['Properties']});pending.extend(refs)
 if len(seen)>80:raise RuntimeError('Unexpected lightweight graph expansion')
edges={x['project']:x['references'] for x in nodes};cycles=[]
def visit(n,path):
 if n in path:cycles.append(path+[n]);return
 for c in edges.get(n,[]):visit(c,path+[n])
visit(str(root/'src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/CanDoItAll.AgentFramework.UiSandbox.csproj'),[])
forbidden=[x['project'] for x in nodes if any(s in x['project'] for s in ['AgentFramework.Core','AgentFramework.Persistence','Modules.AgentFramework','ProviderManagement.Runtime','AgentFramework.Components','CanDoItAll.AppComponents','Voice'])]
result={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'nodes':nodes,'cycles':cycles,'forbidden':forbidden}
(out/f'evaluated-graph-{stage}.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'nodes':len(nodes),'cycles':cycles,'forbidden':forbidden}))
