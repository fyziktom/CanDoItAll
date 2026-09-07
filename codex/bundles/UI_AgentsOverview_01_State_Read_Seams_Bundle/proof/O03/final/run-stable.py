from pathlib import Path
import json,subprocess,datetime,hashlib,re,gzip,sys,xml.etree.ElementTree as E,importlib.util
sys.stdout.reconfigure(encoding='utf-8')
out=Path('.mcp-state/overview03/final');ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
spec=importlib.util.spec_from_file_location('scan','tools/Validation/Portability/scan_artifacts_for_secrets.py');scanner=importlib.util.module_from_spec(spec);spec.loader.exec_module(scanner)
patterns=[re.compile(r'(?<![A-Za-z0-9_-])sk-[A-Za-z0-9_-]{20,}'),re.compile(r'gh[pousr]_[A-Za-z0-9_]{30,}'),re.compile(r'github_pat_[A-Za-z0-9_]{20,}'),re.compile(r'AccountKey=[A-Za-z0-9+/]{60,}={0,2}')]
def scrub(s):
 for pattern in patterns:
  s=pattern.sub(lambda m:'test-only-redacted-'+hashlib.sha256(m.group().encode()).hexdigest()[:16],s)
 for rule,pattern,group in scanner.RULES:
  def replace(m):
   value=m.group(group)
   if scanner.is_placeholder(value):return m.group()
   full=m.group();start=m.start(group)-m.start();end=m.end(group)-m.start()
   return full[:start]+'test-only-redacted-'+scanner.fingerprint(value)+full[end:]
  s=pattern.sub(replace,s)
 return s
assert (out/'measurement-restoration.json').exists(), 'All timing must finish before broad validation.'
command=['dotnet','test','tests/Solutions/CanDoItAll.Tests.Stable.slnx','-c','Release','--no-build','--no-restore','--filter','Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true','/m:1']
r=subprocess.run(command+['--list-tests'],stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
assert r.returncode==0
raw=r.stdout.decode('utf-8-sig')
names=[line.strip() for line in raw.splitlines() if line.startswith('    ') and line.strip().startswith('CanDoItAll.')]
assert names
(out/'stable-discovery.txt').write_text(scrub(raw),encoding='utf-8')
frozen={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'reason':'O02 additive BaseLib navigation-ownership public API plus O03 moved rendering assembly and evaluated project graph.',
 'command':command,'discoverySha256':hashlib.sha256(r.stdout).hexdigest(),'expectedDiscoveryRows':len(names),
 'rows':[{'name':scrub(n),'rawNameSha256':hashlib.sha256(n.encode()).hexdigest()} for n in names],
 'note':'Synthetic secret theory arguments are redacted in evidence before repository scanning. Public FQNs and per-row raw fingerprints remain.'}
(out/'stable-frozen.json').write_text(json.dumps(frozen,indent=2)+'\n',encoding='utf-8')
print('Frozen actual stable discovery',len(names),flush=True)
started=datetime.datetime.now(datetime.timezone.utc).isoformat()
resultsdir=out/'stable-trx';resultsdir.mkdir(exist_ok=True)
assert not list(resultsdir.glob('*.trx')),'Do not overwrite a previous stable run.'
command+=['--logger','trx','--results-directory',str(resultsdir)]
with (out/'stable-result.txt').open('wb') as log:r=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT)
results=[]
executedNames=[]
for f in sorted(resultsdir.glob('*.trx')):
 data=f.read_bytes();t=E.fromstring(data);cases=t.findall('.//t:UnitTestResult',ns)
 executedNames.extend(x.attrib['testName'] for x in cases)
 results.append({'trx':str(f),'rawSha256':hashlib.sha256(data).hexdigest(),'counters':t.find('.//t:Counters',ns).attrib,
 'nonPass':[{'testName':scrub(x.attrib['testName']),'outcome':x.attrib['outcome']} for x in cases if x.attrib['outcome']!='Passed']})
 f.write_text(scrub(data.decode('utf-8-sig')),encoding='utf-8')
p=out/'stable-result.txt';rawOutput=p.read_bytes();p.write_text(scrub(rawOutput.decode('utf-8-sig')),encoding='utf-8')
summary={'startedUtc':started,'finishedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'command':command,'exit':r.returncode,'discoveredRows':len(names),
 'executedCases':len(executedNames),'resultRawSha256':hashlib.sha256(rawOutput).hexdigest(),'results':results}
(out/'stable-summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print(json.dumps(summary),flush=True)
sys.exit(r.returncode)

