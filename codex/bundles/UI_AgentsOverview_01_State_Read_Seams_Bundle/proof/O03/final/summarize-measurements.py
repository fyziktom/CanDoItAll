from pathlib import Path
from collections import Counter
import json,statistics,datetime,hashlib
out=Path('.mcp-state/overview03/final')
assert json.loads((out/'measurement-restoration.json').read_text())['exit']==0
sources=[Path('.mcp-state/overview03-corrected/measurements'),out/'measurements']
samples=[];cold=[];runs=[]
for base in sources:
 for directory in sorted(p for p in base.iterdir() if p.is_dir()):
  ledger=[json.loads(l) for l in (directory/'ledger.jsonl').read_text(encoding='utf-8').splitlines()]
  assert not any(x['kind'] in ['failure','stop-error','restoration-requires-owner-review','asset-isolation-failure'] for x in ledger),str(directory)
  assert any(x['kind']=='complete' for x in ledger),str(directory)
  starts=[x for x in ledger if x['kind']=='owned-process-start'];ends=[x for x in ledger if x['kind']=='owned-process-exit']
  assert {x['pid'] for x in starts}=={x['pid'] for x in ends},str(directory)
  ready=next(x for x in ledger if x['kind']=='interactive-ready')
  if ready['phase']=='cold':cold.append({**ready,'receipt':str(directory/'ledger.jsonl')})
  observed=json.loads((directory/'samples.json').read_text())
  samples.extend({**x,'receipt':str(directory/'samples.json')} for x in observed)
  runs.append({'directory':str(directory),'host':ready['host'],'phase':ready['phase'],'planSha256':ready['planSha256'],'ownedProcesses':starts,'exitReceipts':ends,'samples':len(observed)})
plan=json.loads((out/'planned-edits.json').read_text())
hosts=plan['hosts'];categories=['Razor','C#','CSS'];directions=['forward','reverse']
for host in hosts:
 assert len([x for x in cold if x['host']==host])>=plan['coldRepetitions']
 for edit in plan['edits']:
  for direction in directions:assert len([x for x in samples if x['host']==host and x['editId']==edit['id'] and x['direction']==direction])>=plan['repetitions'],(host,edit['id'],direction)
def stats(values):
 return {'n':len(values),'minimumMs':min(values),'maximumMs':max(values),'rangeMs':max(values)-min(values),'medianMs':statistics.median(values)} if values else {'n':0}
def summarize(rows):
 passing=[x for x in rows if x['success']]
 return {'observations':len(rows),'failures':len(rows)-len(passing),'firstVisible':stats([x['firstVisibleMs'] for x in passing]),'settledVisible':stats([x['settledVisibleMs'] for x in passing]),'classifications':dict(Counter(x['classification'] for x in rows)),'databaseConfirmations':sum(x['databaseConfirmations'] for x in rows),'filterRestorations':sum(x['filterRestorations'] for x in rows)}
result={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'scope':'Corrected pre-owner baseline and final extracted-owner series only. Original pre-move series and failed calibrations/browser attempts are separately retained, never relabeled.','cold':{h:stats([x['elapsedMs'] for x in cold if x['host']==h]) for h in hosts},'byCategory':[],'byEdit':[],'runs':runs,'totalWarm':len(samples),'totalCold':len(cold),'failures':sum(not x['success'] for x in samples),'classificationCounts':dict(Counter(x['classification'] for x in samples))}
for host in hosts:
 for direction in directions:
  for category in categories:result['byCategory'].append({'host':host,'direction':direction,'category':category,**summarize([x for x in samples if x['host']==host and x['direction']==direction and x['category']==category])})
  for edit in plan['edits']:result['byEdit'].append({'host':host,'direction':direction,'edit':edit['id'],**summarize([x for x in samples if x['host']==host and x['direction']==direction and x['editId']==edit['id']])})
(out/'measurement-summary.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
lines=['# Overview direct-watch measurements','','The tables use the corrected pre-extraction owner and final extracted source. All outliers are retained. First-visible and settled observations are separate; settled timing includes the frozen minimum/quiet/actual-chart stability requirements. Cold means a new process using existing restore/filesystem caches, not a cache-cleared machine.','','## Process-cold startup','','| Host | Runs | Minimum ms | Maximum ms | Range ms | Median ms |','|---|---:|---:|---:|---:|---:|']
for host,value in result['cold'].items():lines.append(f"| {host} | {value['n']} | {value['minimumMs']:.3f} | {value['maximumMs']:.3f} | {value['rangeMs']:.3f} | {value['medianMs']:.3f} |")
for direction in directions:
 lines+=['',f'## Warm {direction} observations','','Each category contains three distinct edits, each repeated three times. Exact edit-level distributions are in measurement-summary.json.','','| Host | Edit family | N / failures | First-visible min / max / range / median ms | Settled min / max / range / median ms | Classification |','|---|---|---:|---|---|---|']
 for row in result['byCategory']:
  if row['direction']!=direction:continue
  fmt=lambda v:' / '.join(f"{v[k]:.3f}" for k in ['minimumMs','maximumMs','rangeMs','medianMs'])
  lines.append(f"| {row['host']} | {row['category']} | {row['observations']} / {row['failures']} | {fmt(row['firstVisible'])} | {fmt(row['settledVisible'])} | {row['classifications']} |")
lines+=['','## Interpretation limits','','These are nine specific, supported edits on one Windows machine, SDK 10.0.303 and live sibling source mode. Warm C# or Razor results do not predict unsupported/rude edits or arbitrary application changes. The full app uses the real isolated canonical stores; the sandbox uses their frozen immutable rendering export and has no production runtime. Parity uses production Tailwind input/output; Fast uses its explicit bounded asset entry. Both use real shared controls, CSS isolation, fonts, avatars and Charts assets. The dashboard frame and chart geometry are recorded, with retained expected asset-mode typography differences.','','All owned watch/Tailwind processes have matching exit receipts. Source/asset restoration is separately verified. SDK-driven reloads and any explicit fixture context restoration are recorded per sample. No manual refresh, managed watcher, parallel test/build workload or removed outlier is used. The original baseline before the responsive correction and failed browser/calibration attempts remain outside these corrected-source statistics with their original receipts.','','## Raw totals','',f"{result['totalCold']} cold starts; {result['totalWarm']} warm forward/reverse observations; {result['failures']} observation failures. Classification: {result['classificationCounts']}.",'']
(out/'measurements.md').write_text('\n'.join(lines),encoding='utf-8',newline='\n')
print(json.dumps({'cold':result['cold'],'warm':result['totalWarm'],'failures':result['failures'],'classifications':result['classificationCounts']}))
