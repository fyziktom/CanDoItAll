from pathlib import Path
import subprocess,json,datetime
out=Path('.mcp-state/overview-execution'); b=Path('codex/bundles/UI_AgentsOverview_01_State_Read_Seams_Bundle'); groups=json.loads((b/'inventory/test-inventory.json').read_text())['groups']; records=[]
for g in groups:
 if '/Unit/' in g['project']:g['filter']+='|FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.AgentsOverviewSessionTests|FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.AgentFrameworkModuleChatContextBuilderTests'
 if '/Components/' in g['project']:g['filter']+='|FullyQualifiedName~CanDoItAll.Tests.Components.AgentFramework.AgentsOverviewReadLifecycleTests'
def run(args,name):
 start=datetime.datetime.now(datetime.timezone.utc).isoformat()
 with (out/(name+'.txt')).open('w',encoding='utf-8') as f: r=subprocess.run(args,stdout=f,stderr=subprocess.STDOUT)
 records.append({'name':name,'args':args,'exit':r.returncode,'utc':start}); (out/'o01r2-builds.json').write_text(json.dumps(records,indent=2))
 print(name,r.returncode,flush=True)
 if r.returncode: print((out/(name+'.txt')).read_text()[-5000:]);raise SystemExit(r.returncode)
projects=['src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj','src/App/CanDoItAll.Web/CanDoItAll.Web.csproj']+[g['project'] for g in groups]
for p in projects:run(['dotnet','build',p,'-c','Release','--no-restore','/m:1','/p:UseSharedCompilation=false'],'o01r2-build-'+Path(p).stem)
for g in groups:
 name='o01r2-discovery-'+Path(g['project']).stem
 run(['dotnet','test',g['project'],'-c','Release','--no-build','--no-restore','--list-tests','--filter',g['filter'],'/m:1'],name)
 g['currentDiscoveredNames']=[x.strip() for x in (out/(name+'.txt')).read_text().splitlines() if x.strip().startswith('CanDoItAll.Tests.')]
 assert g['currentDiscoveredNames']
 print('discovered',len(g['currentDiscoveredNames']),flush=True)
(out/'o01r2-discovery.json').write_text(json.dumps(groups,indent=2))
