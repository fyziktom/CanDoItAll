from pathlib import Path
import subprocess,json,datetime,sys
sys.stdout.reconfigure(encoding='utf-8')
out=Path('.mcp-state/overview03');records=[]
projects=[
('src/MAF/Common/CanDoItAll.AgentFramework.Usage/CanDoItAll.AgentFramework.Usage.csproj',None),
('src/UI/CanDoItAll.Conversations.Components/CanDoItAll.Conversations.Components.csproj',None),
('C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.Charts/CanDoItAll.Components.Charts.csproj',None),
('src/UI/CanDoItAll.AgentFramework.UI/CanDoItAll.AgentFramework.UI.csproj',None),
('src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj',None),
('src/App/CanDoItAll.Web/CanDoItAll.Web.csproj',None),
('src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/CanDoItAll.AgentFramework.UiSandbox.csproj','Parity'),
('src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/CanDoItAll.AgentFramework.UiSandbox.csproj','Fast'),
('tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj',None),
('tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj',None),
('tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj',None),
('.mcp-state/overview-browser-fixture/OverviewBrowser.csproj',None)]
for project,mode in projects:
 name='build-'+Path(project).stem+('-'+mode if mode else '')
 command=['dotnet','build',project,'-c','Release','--nologo','/m:1','/p:UseSharedCompilation=false','-v:q']+(['-p:CatalogAssetMode='+mode] if mode else [])
 start=datetime.datetime.now(datetime.timezone.utc).isoformat()
 with (out/(name+'.txt')).open('w',encoding='utf-8') as log:r=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT)
 records.append({'project':project,'mode':mode,'command':command,'exit':r.returncode,'startedUtc':start,'finishedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat()})
 (out/'direct-builds.json').write_text(json.dumps(records,indent=2)+'\n',encoding='utf-8')
 print(name,r.returncode,flush=True)
 if r.returncode:
  print((out/(name+'.txt')).read_text(encoding='utf-8')[-8000:]);sys.exit(r.returncode)
