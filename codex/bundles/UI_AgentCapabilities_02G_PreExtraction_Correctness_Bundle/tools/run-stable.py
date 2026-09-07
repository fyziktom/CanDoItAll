from pathlib import Path
import json,subprocess,sys,datetime,xml.etree.ElementTree as E
sys.stdout.reconfigure(encoding='utf-8')
out=Path('.mcp-state/capa02g'); b=Path('codex/bundles/UI_AgentCapabilities_02G_PreExtraction_Correctness_Bundle/proof')
for name in ['unit','components','integration']:
 j=json.loads((out/('final-owning-'+name+'-summary.json')).read_text(encoding='utf-8'))
 if j['counters']['failed']!='0' or j['counters']['notExecuted']!='0': raise RuntimeError('Owning gate is not passing')
names=[l.strip() for l in (out/'stable-discovery.txt').read_text(encoding='utf-8-sig').splitlines() if l.startswith('    ') and l.strip().startswith('CanDoItAll.')]
if len(names)!=9951: raise RuntimeError('Stable discovery changed')
cmd=['dotnet','test','tests/Solutions/CanDoItAll.Tests.Stable.slnx','-c','Release','--no-build','--no-restore','--filter','Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true','/m:1','--logger','trx','--results-directory',str(out/'stable-trx')]
(out/'stable-frozen.json').write_text(json.dumps({'expectedCases':len(names),'names':names,'command':cmd},indent=2)+'\n',encoding='utf-8')
started=datetime.datetime.now(datetime.timezone.utc).isoformat()
with (out/'stable-result.txt').open('wb') as log: p=subprocess.run(cmd,stdout=log,stderr=subprocess.STDOUT)
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
results=[]
for f in sorted((out/'stable-trx').glob('*.trx')):
 t=E.parse(f);results.append({'trx':str(f),'counters':t.find('.//t:Counters',ns).attrib,'nonPass':[{k:v for k,v in x.attrib.items() if k in ['testName','outcome']} for x in t.findall('.//t:UnitTestResult',ns) if x.attrib['outcome']!='Passed']})
r={'startedUtc':started,'finishedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'command':cmd,'exit':p.returncode,'discovered':len(names),'results':results}
(out/'stable-summary.json').write_text(json.dumps(r,indent=2)+'\n',encoding='utf-8'); print(json.dumps(r));sys.exit(p.returncode)
