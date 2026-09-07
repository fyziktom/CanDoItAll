from pathlib import Path
import subprocess,json,datetime,os,sys,time
out=Path('.mcp-state/capa03');env=os.environ.copy();env['CAPA_PLAYWRIGHT']=r'C:\Users\lucys\AppData\Local\npm-cache\_npx\e41f203b7505f1fb\node_modules\playwright'
results=[]
for phase,repetitions in [('cold',3),('warm',1)]:
 for repetition in range(1,repetitions+1):
  command=['node',str(out/'direct-watch.cjs'),'--plan',str(out/'planned-edits.json'),'--host','pre-fullapp','--phase',phase,'--repetition',str(repetition),'--environment','.mcp-state/capa03-data/environment.json','--output',str(out/'measurements')]
  started=datetime.datetime.now(datetime.timezone.utc).isoformat();print('Starting',phase,repetition,started,flush=True)
  r=subprocess.run(command,env=env)
  results.append({'phase':phase,'repetition':repetition,'command':command,'exit':r.returncode,'startedUtc':started,'finishedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat()})
  (out/'sb00-measurement-runs.json').write_text(json.dumps(results,indent=2)+'\n',encoding='utf-8')
  if r.returncode:sys.exit(r.returncode)
  time.sleep(3)
