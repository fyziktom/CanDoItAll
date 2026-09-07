from pathlib import Path
import subprocess,json,datetime,sys
out=Path('.mcp-state/overview03/final')
restoration=json.loads((out/'measurement-restoration.json').read_text())
assert restoration['exit']==0 and all(x['restored'] for x in restoration['records'])
commands=[['dotnet','restore','CanDoItAll.slnx'],['dotnet','build','CanDoItAll.slnx','-c','Release','--no-restore','/m:1'],['dotnet','restore','tests/Solutions/CanDoItAll.Tests.Stable.slnx'],['dotnet','build','tests/Solutions/CanDoItAll.Tests.Stable.slnx','-c','Release','--no-restore','/m:1']]
records=[]
for i,command in enumerate(commands):
 started=datetime.datetime.now(datetime.timezone.utc).isoformat();print('Stable prerequisite',i+1,flush=True)
 with (out/f'stable-prerequisite-{i+1}.txt').open('w',encoding='utf-8') as log:r=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT)
 records.append({'command':command,'exit':r.returncode,'startedUtc':started,'finishedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat()})
 (out/'stable-prerequisites.json').write_text(json.dumps(records,indent=2)+'\n',encoding='utf-8')
 if r.returncode:sys.exit(r.returncode)
sys.exit(subprocess.run(['python',str(out/'run-stable.py')]).returncode)
