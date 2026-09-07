from pathlib import Path
import json, statistics, collections, csv, hashlib, datetime
root=Path.cwd(); state=root/'.mcp-state/capa03'; measured=state/'measurements'
plan=json.loads((state/'planned-edits-post.json').read_text(encoding='utf-8'))
pre=json.loads((state/'sb00-measurement-summary.json').read_text(encoding='utf-8'))
records=json.loads((state/'sb03-responsive-runs.json').read_text(encoding='utf-8'))
assert len(records)==15 and all(x['exit']==0 for x in records), 'Review incomplete or failed driver runs before accepting measurements.'
def ledger(folder):
 return [json.loads(line) for line in (folder/'ledger.jsonl').read_text(encoding='utf-8').splitlines() if line.strip()]
def moment(s):
 return datetime.datetime.fromisoformat(s.replace('Z','+00:00'))
def accepted_folder(record):
 matches=[]
 for folder in measured.glob(record['host']+'-'+record['phase']+'-*'):
  rows=ledger(folder)
  if rows and moment(record['startedUtc']) <= moment(rows[0]['utc']) <= moment(record['finishedUtc']):
   assert any(x['kind']=='complete' for x in rows) and not any(x['kind']=='failure' for x in rows)
   matches.append(folder)
 assert len(matches)==1, (record, matches)
 return matches[0]
def stats(values):
 return {'minMs':min(values),'maxMs':max(values),'rangeMs':max(values)-min(values),'medianMs':statistics.median(values)}
accepted={host:{'cold':[], 'warm':None, 'calibrate':[]} for host in plan['hosts']}
accepted['pre-fullapp']['cold']=[measured/x['run'] for x in pre['cold']]
accepted['pre-fullapp']['warm']=measured/pre['warmRun']
for record in records:
 folder=accepted_folder(record)
 if record['phase']=='warm':
  assert accepted[record['host']]['warm'] is None
  accepted[record['host']]['warm']=folder
 else:accepted[record['host']][record['phase']].append(folder)
expected={(e['id'],rep,direction) for e in plan['edits'] for rep in range(1,plan['repetitions']+1) for direction in ['forward','reverse']}
output={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'planSha256':hashlib.sha256((state/'planned-edits-post.json').read_bytes()).hexdigest(),'hosts':[], 'sourceRestoration':[]}
flat=[]
for host, runs in accepted.items():
 assert len(runs['cold'])==plan['coldRepetitions'] and runs['warm']
 cold=[]
 for folder in runs['cold']:
  ready=[x for x in ledger(folder) if x['kind']=='interactive-ready']
  assert len(ready)==1
  cold.append({'run':folder.name,**ready[0]})
 samples=json.loads((runs['warm']/'samples.json').read_text(encoding='utf-8'))
 assert len(samples)==len(expected)
 assert {(x['editId'],x['repetition'],x['direction']) for x in samples}==expected
 assert all(x['success'] for x in samples)
 groups=[]
 for category in ['Razor','C#','CSS']:
  for direction in ['forward','reverse']:
   group=[x for x in samples if x['category']==category and x['direction']==direction]
   groups.append({'category':category,'direction':direction,'observations':len(group),'failures':sum(not x['success'] for x in group),
    'firstVisible':stats([x['firstVisibleMs'] for x in group]),'settledVisible':stats([x['settledVisibleMs'] for x in group]),
    'classifications':dict(collections.Counter(x['classification'] for x in group)),
    'documentChanges':sum(x['documentBefore']!=x['documentAfter'] for x in group),
    'runtimeChanges':sum(x['before']['runtimePid']!=x['after']['runtimePid'] for x in group),
    'browserEvents':dict(collections.Counter((e.get('type') or e['kind']) for x in group for e in x['browserEvents']))})
 edits=[]
 for edit in plan['edits']:
  for direction in ['forward','reverse']:
   group=[x for x in samples if x['editId']==edit['id'] and x['direction']==direction]
   edits.append({'editId':edit['id'],'category':edit['category'],'direction':direction,'observations':len(group),
    'firstVisible':stats([x['firstVisibleMs'] for x in group]),'settledVisible':stats([x['settledVisibleMs'] for x in group]),
    'classifications':dict(collections.Counter(x['classification'] for x in group)),
    'documentChanges':sum(x['documentBefore']!=x['documentAfter'] for x in group),
    'runtimeChanges':sum(x['before']['runtimePid']!=x['after']['runtimePid'] for x in group),
    'browserEvents':dict(collections.Counter((e.get('type') or e['kind']) for x in group for e in x['browserEvents']))})
 output['hosts'].append({'host':host,'cold':cold,'coldSummary':stats([x['elapsedMs'] for x in cold]),'warmRun':runs['warm'].name,'groups':groups,'edits':edits,'calibrationRuns':[x.name for x in runs['calibrate']]})
 for x in samples:
  flat.append({key:x[key] for key in ['host','editId','category','repetition','direction','utc','success','classification','firstVisibleMs','settledVisibleMs','flushedSha256']})
for name,sha in plan['postHashes'].items():
 actual=hashlib.sha256((root/name).read_bytes()).hexdigest()
 assert actual==sha, 'A measurement source is not restored: '+name
 output['sourceRestoration'].append({'path':name,'sha256':actual})
(state/'measurement-comparison.json').write_text(json.dumps(output,indent=2)+'\n',encoding='utf-8')
with (state/'measurement-samples.csv').open('w',encoding='utf-8',newline='') as f:
 writer=csv.DictWriter(f,fieldnames=list(flat[0]));writer.writeheader();writer.writerows(flat)
lines=['# Direct watch results','', 'All figures are direct source-flush-to-visible observations on the frozen machine, SDK, browser and live sibling graph. Process-cold startup includes the normal interactive fixture gate and populated restore/filesystem caches. It is separate from warm editing.','', '## Process-cold startup','', '| Host | Runs | Minimum (s) | Maximum (s) | Range (s) | Median (s) |','|---|---:|---:|---:|---:|---:|']
for h in output['hosts']:
 s=h['coldSummary'];lines.append(f"| {h['host']} | {len(h['cold'])} | {s['minMs']/1000:.3f} | {s['maxMs']/1000:.3f} | {s['rangeMs']/1000:.3f} | {s['medianMs']/1000:.3f} |")
for direction in ['forward','reverse']:
 lines+=['', '## '+direction.capitalize()+' warm observations','', '| Host | Category | Count | First visible min / max / range / median (ms) | Settled median (ms) | Classification |','|---|---|---:|---|---:|---|']
 for h in output['hosts']:
  for g in h['groups']:
   if g['direction']!=direction:continue
   s=g['firstVisible'];classes=', '.join(f'{k}: {v}' for k,v in g['classifications'].items())
   lines.append(f"| {h['host']} | {g['category']} | {g['observations']} | {s['minMs']:.1f} / {s['maxMs']:.1f} / {s['rangeMs']:.1f} / {s['medianMs']:.1f} | {g['settledVisible']['medianMs']:.1f} | {classes} |")
lines+=['','Each category contains three distinct frozen edits, each repeated three times. Reverse observations are restoration measurements, not extra forward repetitions. Per-edit min/max/range/median, settled ranges, cold raw rows and exact restored hashes are in measurement-comparison.json; every warm row is in measurement-samples.csv.','', 'The classifier groups SDK browser refresh/enhanced navigation under browser-reload; inspect document identity and browser events before claiming a full document reload. CSS hot-reload is the observed static-asset update, not proof of a managed code delta. Settled observations include the fixed 1.5-second minimum and quiet-window protocol, so they are deliberately conservative confirmation time.','', 'Historical protocol-v1 cold attempts and the interrupted pre-move warm run remain excluded with their original evidence. The first complete post-move series is excluded because sandbox responsive utilities were missing; responsive-utility-adjudication.md records the direct RED and correction. These accepted rows come from the complete repeat with the corrected assets. No performance conclusion is inferred automatically from these numbers.','']
(state/'measurement-results.md').write_text('\n'.join(lines),encoding='utf-8')
print(json.dumps({'hosts':len(output['hosts']),'warmObservations':len(flat),'restored':len(output['sourceRestoration'])}))
