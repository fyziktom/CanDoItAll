from pathlib import Path
import subprocess,json,datetime,sys
out=Path('.mcp-state/capa03');records=[]
projects=[('src/UI/CanDoItAll.AgentFramework.UI/CanDoItAll.AgentFramework.UI.csproj',None),('src/MAF/Common/CanDoItAll.AgentFramework.Components/CanDoItAll.AgentFramework.Components.csproj',None),('src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj',None),('src/App/CanDoItAll.Web/CanDoItAll.Web.csproj',None),('src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/CanDoItAll.AgentFramework.UiSandbox.csproj','Parity'),('src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/CanDoItAll.AgentFramework.UiSandbox.csproj','Fast'),('tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj',None),('tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj',None)]
for project,mode in projects:
 name=Path(project).stem+('-'+mode if mode else '')
 command=['dotnet','build',project,'-c','Release','--no-restore','/m:1','--nologo','-v:q']+(['--property:CatalogAssetMode='+mode] if mode else [])
 started=datetime.datetime.now(datetime.timezone.utc).isoformat()
 with (out/('sb00-build-'+name+'.txt')).open('wb') as log:r=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT)
 records.append({'command':command,'exit':r.returncode,'startedUtc':started,'finishedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'log':'sb00-build-'+name+'.txt'})
 (out/'sb00-builds.json').write_text(json.dumps(records,indent=2)+'\n',encoding='utf-8')
 print(name,r.returncode,flush=True)
 if r.returncode:sys.exit(r.returncode)
