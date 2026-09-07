from pathlib import Path
import json,hashlib,subprocess,datetime,os,sys,time
root=Path.cwd();out=Path('.mcp-state/overview03/final');out.mkdir(exist_ok=True)
assert (out/'owning-selections.json').exists(),'Final build/test driver must complete first.'
pre=json.loads(Path('.mcp-state/overview03-corrected/replay-receipt.json').read_text())
assert pre['postOwnerRestored'] and pre['status']=='VALID CORRECTED PRE-MOVE BASELINE'
sha=lambda b:hashlib.sha256(b).hexdigest()
assets=['src/App/CanDoItAll.Web/wwwroot/css/output.css','src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/wwwroot/css/catalog-fast.css']
for input,output in zip(['Tailwind/input.css','Tailwind/catalog-fast.css'],assets):
 command=['node','Tailwind/node_modules/@tailwindcss/cli/dist/index.mjs','-i',input,'-o',output]
 with (out/(Path(input).stem+'-asset-freeze.txt')).open('w',encoding='utf-8') as log:r=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT)
 assert r.returncode==0
plan=json.loads(Path('.mcp-state/overview03-corrected/planned-edits.json').read_text())
plan['status']='Frozen post-extraction comparison against corrected pre-owner baseline; same protocol v2 and canonical fixture.'
plan['entryUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
plan['postHashes']={e['postPath']:sha(Path(e['postPath']).read_bytes()) for e in plan['edits']}
files=set()
for item in plan['sourceInventory']:
 if Path(item['path']).is_file():files.add(item['path'])
for base in ['src/UI/CanDoItAll.AgentFramework.UI','src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox']:
 for p in Path(base).rglob('*'):
  if p.is_file() and not any(x in p.parts for x in ['bin','obj']):files.add(p.as_posix())
plan['sourceInventory']=[{'path':p,'sha256':sha(Path(p).read_bytes()),'bytes':Path(p).stat().st_size} for p in sorted(files)]
for move in json.loads(Path('.mcp-state/overview03/moves.json').read_text())['moves']:
 assert not Path(move['from']).exists(),move['from']
 assert Path(move['to']).is_file(),move['to']
assert sha(Path('.mcp-state/overview03/direct-watch.cjs').read_bytes())==plan['harnessSha256']
(out/'planned-edits.json').write_text(json.dumps(plan,indent=2)+'\n',encoding='utf-8')
restore={p:Path(p).read_bytes() for p in list(plan['postHashes'])+assets}
backup=out/'restoration-bytes';backup.mkdir(exist_ok=True)
for p,b in restore.items():
 target=backup/p;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
env=os.environ.copy();env['OVERVIEW_PLAYWRIGHT']=r'C:\Users\lucys\AppData\Local\npm-cache\_npx\e41f203b7505f1fb\node_modules\playwright'
results=[];code=0
try:
 for host in ['post-fullapp','parity','fast']:
  for phase,repetitions in [('cold',3),('warm',1)]:
   for repetition in range(1,repetitions+1):
    command=['node','.mcp-state/overview03/direct-watch.cjs','--plan',str(out/'planned-edits.json'),'--host',host,'--phase',phase,'--repetition',str(repetition),'--environment','.mcp-state/overview03-data/environment.json','--output',str(out/'measurements')]
    started=datetime.datetime.now(datetime.timezone.utc).isoformat();print('Starting',host,phase,repetition,started,flush=True)
    with (out/f'{host}-{phase}-{repetition}.txt').open('w',encoding='utf-8') as log:r=subprocess.run(command,env=env,stdout=log,stderr=subprocess.STDOUT)
    results.append({'host':host,'phase':phase,'repetition':repetition,'command':command,'exit':r.returncode,'startedUtc':started,'finishedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat()})
    (out/'measurement-runs.json').write_text(json.dumps(results,indent=2)+'\n',encoding='utf-8')
    print('Finished',host,phase,repetition,'exit',r.returncode,flush=True)
    if r.returncode:raise RuntimeError(f'{host} {phase} failed; retain and review before rerun.')
    time.sleep(3)
except Exception as error:
 code=1;print(type(error).__name__,str(error),flush=True)
finally:
 records=[]
 for p,b in restore.items():
  actual=Path(p).read_bytes()
  if p not in assets:
   assert actual==b,'Source was not restored: '+p
  elif actual!=b:
   Path(p).write_bytes(b)
  records.append({'path':p,'expectedSha256':sha(b),'observedBeforeRestorationSha256':sha(actual),'finalSha256':sha(Path(p).read_bytes()),'restored':Path(p).read_bytes()==b})
 (out/'measurement-restoration.json').write_text(json.dumps({'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'records':records,'exit':code},indent=2)+'\n',encoding='utf-8')
sys.exit(code)
