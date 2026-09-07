from pathlib import Path
import json,gzip,hashlib,datetime,collections,xml.etree.ElementTree as E,subprocess
root=Path.cwd()
scratch=root/'.mcp-state/overview01d/stable'
bundle=root/'codex/bundles/UI_AgentsOverview_01D_PostPush_Delivery_Closure_Bundle'
summary=json.loads((scratch/'stable-summary.json').read_text(encoding='utf-8'))
frozen=json.loads((scratch/'stable-frozen.json').read_text(encoding='utf-8'))
assert summary['exit']==0, 'Stable execution must succeed before retention closure.'
assert summary['executedCases']==10254 and len(summary['results'])==5
for result in summary['results']:
    assert not result['nonPass']
    assert int(result['counters']['executed'])==int(result['counters']['passed'])
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
discovered=[r['name'] for r in frozen['rows']]
executed=[]
for file in sorted((scratch/'stable-trx').glob('*.trx')):
    executed += [r.attrib['testName'] for r in E.parse(file).findall('.//t:UnitTestResult',ns)]
def group(rows):
    result=collections.defaultdict(collections.Counter)
    for name in rows:result[name.split('(')[0]][name]+=1
    return result
a,b=group(discovered),group(executed)
assert set(a)==set(b), 'Discovered and executed method identities differ.'
old=json.loads((root/'codex/bundles/UI_AgentsOverview_01_State_Read_Seams_Bundle/proof/O03/final/stable-discovery-verification.json').read_text(encoding='utf-8'))
known={r['method']:r for r in old['adjudicatedMethods']}
differences=[]
for method in sorted(a):
    if a[method]==b[method]:continue
    prior=known.get(method)
    assert prior is not None, 'New unexplained discovery display/count difference: '+method
    assert len(list(a[method].elements()))==prior['discoveredRows']
    assert len(list(b[method].elements()))==prior['executedRows']
    assert hashlib.sha256((root/prior['source']).read_bytes()).hexdigest()==prior['sourceSha256']
    differences.append(prior)
verification={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'pass':True,'discoveredEntries':len(discovered),'executedCases':len(executed),'allMethodIdentitiesAccounted':True,'allOtherExpandedNamesEqual':True,'additionalRuntimeCases':len(executed)-len(discovered),'adjudicatedMethods':differences,'qualification':'Fresh discovery and fresh TRX compared. Every exceptional method has the same row counts and source bytes as its explicit historical deferred-data/display-sanitization adjudication. Historical runtime results are not reused.'}
assert verification['additionalRuntimeCases']==55
out=bundle/'retained/A06/stable'
out.mkdir(exist_ok=True)
for name in ['stable-frozen.json','stable-discovery.txt','stable-result.txt']:
    (out/(name+'.gz')).write_bytes(gzip.compress((scratch/name).read_bytes(),mtime=0))
(out/'stable-summary.json').write_bytes((scratch/'stable-summary.json').read_bytes())
(out/'discovery-verification.json').write_text(json.dumps(verification,indent=2)+'\n',encoding='utf-8',newline='\n')
for file in sorted((scratch/'stable-trx').glob('*.trx')):
    (out/(file.name+'.gz')).write_bytes(gzip.compress(file.read_bytes(),mtime=0))
print(json.dumps({'stableExit':0,'discovered':len(discovered),'executed':len(executed),'methodIdentities':len(a),'adjudicatedExceptions':len(differences),'retained':str(out)}))
