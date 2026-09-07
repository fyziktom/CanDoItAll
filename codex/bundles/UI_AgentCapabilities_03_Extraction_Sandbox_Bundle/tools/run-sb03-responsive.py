from pathlib import Path
import json,subprocess,datetime,time,sys,os,hashlib
sys.stdout.reconfigure(encoding='utf-8')
root=Path.cwd();state=root/'.mcp-state/capa03';summary=state/'stable-summary.json'
print('Waiting for the required stable gate; no measurement host is running.',flush=True)
while not summary.exists():
 time.sleep(15)
gate=json.loads(summary.read_text(encoding='utf-8'))
assert gate['exit']==0 and all(x['counters']['failed']=='0' and x['counters']['notExecuted']=='0' for x in gate['results']), 'Stable gate failed; measurements remain blocked.'
assert 'Status: PASS' in (root/'codex/bundles/UI_AgentCapabilities_03_Extraction_Sandbox_Bundle/proof/SB02/closure.md').read_text(encoding='utf-8')
plan=state/'planned-edits-post.json';data=json.loads(plan.read_text(encoding='utf-8'))
for path,sha in data['postHashes'].items():
 assert hashlib.sha256((root/path).read_bytes()).hexdigest()==sha, 'Post-source changed before measurement.'
for mode in ['Parity','Fast']:
 assert json.loads((state/'browser-responsive-cascade-green'/mode/'summary.json').read_text(encoding='utf-8'))['status']=='PASS'
for kind in ['Unit','Components']:
 assert json.loads((state/('cascade-'+kind+'-summary.json')).read_text(encoding='utf-8'))['counters']['failed']=='0'
for path,sha in data['responsiveUtilityClosure']['sourceInputs'].items():
 assert hashlib.sha256((root/path).read_bytes()).hexdigest()==sha
build=['dotnet','build','src/App/CanDoItAll.Web/CanDoItAll.Web.csproj','-c','Release','/m:1','-v:q']
with (state/'sb03-responsive-web-assets-build.txt').open('wb') as log:
 result=subprocess.run(build,stdout=log,stderr=subprocess.STDOUT)
assert result.returncode==0
env=os.environ.copy()
env['CAPA_PLAYWRIGHT']=r'C:\Users\lucys\AppData\Local\npm-cache\_npx\e41f203b7505f1fb\node_modules\playwright'
records=[]
for host in ['post-fullapp','parity','fast']:
 for phase,repetition in [('calibrate',1),('cold',1),('cold',2),('cold',3),('warm',1)]:
  command=['node',str(state/'direct-watch.cjs'),'--plan',str(plan),'--host',host,'--phase',phase,'--repetition',str(repetition),'--output',str(state/'measurements')]
  if host.endswith('fullapp'):
   command+=['--environment',str(root/'.mcp-state/capa03-data/environment.json')]
  started=datetime.datetime.now(datetime.timezone.utc).isoformat()
  print('START',host,phase,repetition,started,flush=True)
  logpath=state/f'sb03-responsive-{host}-{phase}-{repetition}.txt'
  with logpath.open('wb') as log:
   result=subprocess.run(command,env=env,stdout=log,stderr=subprocess.STDOUT)
  row={'host':host,'phase':phase,'repetition':repetition,'command':command,'startedUtc':started,
      'finishedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'exit':result.returncode,'log':logpath.name}
  records.append(row)
  (state/'sb03-responsive-runs.json').write_text(json.dumps(records,indent=2)+'\n',encoding='utf-8')
  print('END',host,phase,repetition,result.returncode,flush=True)
  if result.returncode:sys.exit(result.returncode)
print('All post-extraction direct-watch configurations completed.',flush=True)

