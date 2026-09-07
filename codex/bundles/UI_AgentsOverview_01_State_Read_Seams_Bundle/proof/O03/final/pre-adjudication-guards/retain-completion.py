from pathlib import Path
import gzip,json,shutil,hashlib,datetime,collections,xml.etree.ElementTree as E
root=Path.cwd();state=root/'.mcp-state/overview03';out=state/'final';proof=root/'codex/bundles/UI_AgentsOverview_01_State_Read_Seams_Bundle/proof/O03'
summary=json.loads((out/'stable-summary.json').read_text())
assert summary['exit']==0 and summary['executedCases']==summary['discoveredRows']
assert all(not x['nonPass'] for x in summary['results'])
hygiene=json.loads((out/'hygiene/receipt.json').read_text());assert hygiene['status'].startswith('PASS')
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
frozen=json.loads((out/'stable-frozen.json').read_text());expected=[r['name'] for r in frozen['rows']];actual=[]
for p in (out/'stable-trx').glob('*.trx'):
 t=E.parse(p);actual.extend(x.attrib['testName'] for x in t.findall('.//t:UnitTestResult',ns))
missing=collections.Counter(expected)-collections.Counter(actual);extra=collections.Counter(actual)-collections.Counter(expected)
assert not missing and not extra, 'Final stable expanded discovery differs from the executed names.'
(out/'stable-discovery-verification.json').write_text(json.dumps({'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'discovered':len(expected),'executed':len(actual),'exactExpandedNamesMatch':True,'missing':[],'extra':[]},indent=2)+'\n',encoding='utf-8')
rows=[]
def retain(source,dest):
 assert source.is_file();dest.parent.mkdir(parents=True,exist_ok=True)
 data=source.read_bytes()
 if source.suffix.lower() in {'.txt','.log','.trx','.html','.jsonl'}:
  dest=dest.with_name(dest.name+'.gz');dest.write_bytes(gzip.compress(data,mtime=0))
 else:dest.write_bytes(data)
 rows.append({'source':source.relative_to(root).as_posix(),'artifact':dest.relative_to(proof).as_posix(),'sourceSha256':hashlib.sha256(data).hexdigest(),'retainedSha256':hashlib.sha256(dest.read_bytes()).hexdigest()})
for p in sorted(out.glob('stable-*')):
 if p.is_file():retain(p,proof/'final'/p.name)
for p in sorted((out/'stable-trx').glob('*.trx')):retain(p,proof/'final/stable-trx'/p.name)
for p in sorted((out/'hygiene').rglob('*')):
 if p.is_file():retain(p,proof/'final/hygiene'/p.relative_to(out/'hygiene'))
for p in sorted((state/'browser-web-hygiene').glob('*')):
 if p.is_file():retain(p,proof/'browser-web-hygiene'/p.name)
for name in ['run-broad.py','run-stable.py','finish-hygiene.py','focused-discovery-verification.json','focused-execution-overlap.json','owned-ports-before-hygiene.json']:
 retain(out/name,proof/'final'/name)
(proof/'final-completion-retention.json').write_text(json.dumps({'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'records':rows},indent=2)+'\n',encoding='utf-8')
print(json.dumps({'retainedFiles':len(rows),'stableCases':len(actual),'expandedNamesMatch':True,'hygiene':hygiene['status']}))
