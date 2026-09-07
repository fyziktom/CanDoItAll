from pathlib import Path
import subprocess,json,datetime,sys
r=Path.cwd();out=r/'.mcp-state/capa02g'; phase=sys.argv[1]
projects=json.loads((out/'red-builds.json').read_text(encoding='utf-8'))
extras = ['src/MAF/Common/CanDoItAll.AgentFramework.Persistence/CanDoItAll.AgentFramework.Persistence.csproj', 'src/UI/CanDoItAll.AgentFramework.UI/CanDoItAll.AgentFramework.UI.csproj']
projects[1:1] = [{'command':['dotnet','build',project,'-c','Release','--no-restore','/m:1','--nologo','-v:q']} for project in extras]
entries=[]
for previous in projects:
 command=previous['command'];path=out/(phase+'-build-'+Path(command[2]).stem+'.txt')
 start=datetime.datetime.now(datetime.timezone.utc).isoformat()
 with path.open('w',encoding='utf-8') as stream: result=subprocess.run(command,stdout=stream,stderr=subprocess.STDOUT)
 entries.append({'command':command,'exit':result.returncode,'startedUtc':start,'finishedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'log':path.name})
 (out/(phase+'-builds.json')).write_text(json.dumps(entries,indent=2)+'\n',encoding='utf-8')
 print(command[2],result.returncode,flush=True)
 if result.returncode:
  sys.stdout.reconfigure(encoding='utf-8');print(path.read_text(encoding='utf-8')[-5500:]);sys.exit(result.returncode)
