from pathlib import Path
import subprocess,json,datetime
out=Path('.mcp-state/overview-execution'); records=[]
projects=[r'C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj',r'C:/repositories/CanDoItAll.Components/tests/CanDoItAll.Components.BaseLib.Tests/CanDoItAll.Components.BaseLib.Tests.csproj','src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj','tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj','tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj','src/App/CanDoItAll.Web/CanDoItAll.Web.csproj']
for p in projects:
 name='o02-build-'+Path(p).stem
 args=['dotnet','build',p,'-c','Release','--no-restore','/m:1','/p:UseSharedCompilation=false']
 with (out/(name+'.txt')).open('w',encoding='utf-8') as f:r=subprocess.run(args,stdout=f,stderr=subprocess.STDOUT)
 records.append({'project':p,'args':args,'exit':r.returncode,'utc':datetime.datetime.now(datetime.timezone.utc).isoformat()})
 (out/'o02-builds.json').write_text(json.dumps(records,indent=2))
 print(name,r.returncode,flush=True)
 if r.returncode:
  print((out/(name+'.txt')).read_text(encoding='utf-8')[-6000:]);raise SystemExit(r.returncode)
