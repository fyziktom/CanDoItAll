from pathlib import Path
import subprocess,json,gzip,datetime,re,hashlib
root=Path.cwd();scratch=root/'.mcp-state/overview01d';bundle=root/'codex/bundles/UI_AgentsOverview_01D_PostPush_Delivery_Closure_Bundle'
assert (bundle/'retained/A06/stable/stable-summary.json').exists()
out=bundle/'retained/A06/closure-checks';out.mkdir(exist_ok=True)
commands=[]
def run(name,command,expected=0):
    start=datetime.datetime.now(datetime.timezone.utc).isoformat()
    p=subprocess.run(command,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
    (out/(name+'.txt.gz')).write_bytes(gzip.compress(p.stdout,mtime=0))
    commands.append({'name':name,'command':command,'exit':p.returncode,'startedUtc':start,'finishedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat()})
    (out/'commands.json').write_text(json.dumps(commands,indent=2)+'\n',encoding='utf-8',newline='\n')
    print(name,p.returncode,flush=True)
    assert p.returncode==expected,name+' failed its required result'
    return p.stdout
run('source-secrets',['python','.mcp-state/overview01d/scan-source.py'])
source=json.loads((scratch/'source-secret-scan.json').read_text(encoding='utf-8'))
assert not source['added']
(out/'source-secret-scan.json.gz').write_bytes(gzip.compress((scratch/'source-secret-scan.json').read_bytes(),mtime=0))
run('components-source-secrets',['python','.mcp-state/overview01d/scan-components-source.py'])
components=json.loads((scratch/'components-source-secret-final.json').read_text(encoding='utf-8'))
assert not components['added']
(out/'components-source-secret-scan.json.gz').write_bytes(gzip.compress((scratch/'components-source-secret-final.json').read_bytes(),mtime=0))
bundles=['UI_AgentsOverview_01_State_Read_Seams_Bundle','UI_AgentGovernance_01_State_Read_Seams_Bundle','UI_AgentsOverview_01D_PostPush_Delivery_Closure_Bundle']
for name,scope in zip(['overview','governance','delivery'],bundles):
    result=scratch/(name+'-retained-final.json')
    run(name+'-retained-secrets',['python','.mcp-state/overview-execution/scan-retained.py','codex/bundles/'+scope,str(result)])
    (out/(name+'-retained-secrets.json.gz')).write_bytes(gzip.compress(result.read_bytes(),mtime=0))
command=['python','tools/Validation/bundles/scan_text_encoding.py','--changed-since','ad2ded645e65b8b205959f6fed816d082b99a7be','--scope','src','--scope','tools/Validation/bundles']
for bundleName in bundles:command+=['--scope','codex/bundles/'+bundleName]
command+=['--output',str(scratch/'encoding-final.json')]
run('encoding',command)
(out/'encoding.json').write_bytes((scratch/'encoding-final.json').read_bytes())
run('components-encoding',['python','tools/Validation/bundles/scan_text_encoding.py','--repo-root','../CanDoItAll.Components','--changed-since','HEAD','--scope','tests/CanDoItAll.Components.BaseLib.Tests/DialogNavigationOwnershipTests.cs','--output',str(scratch/'components-encoding-final.json')])
(out/'components-encoding.json').write_bytes((scratch/'components-encoding-final.json').read_bytes())
run('portability-enforcement',['python','tools/Validation/Portability/enforce_portability_baseline.py','--scan',str(scratch/'portability-final-r3.json'),'--baseline','tools/Validation/Portability/portability-risk-baseline.json'])
current=run('documentation',['powershell','-NoProfile','-ExecutionPolicy','Bypass','-File','tools/Validation/Test-Documentation.ps1'],1)
prior=gzip.decompress((bundle/'retained/A06/static-final/documentation.txt.gz').read_bytes())
def errors(data):
    return [line for line in data.decode('utf-8-sig').splitlines() if line.startswith('ERROR:')]
assert errors(prior)==errors(current) and len(errors(current))==1, 'Documentation failure set changed.'
assert '118 file(s)' in errors(current)[0]
def prohibited(raw):
    return {p for p in raw.decode('utf-8').split('\0') if re.search(r'\.(?:log|pyc|pid)$',p)}
a=prohibited(subprocess.check_output(['git','ls-tree','-rz','--name-only','HEAD']))
b=prohibited(subprocess.check_output(['git','ls-files','-z']))
assert len(a)==118 and a==b and all((root/p).is_file() for p in b)
assert not subprocess.check_output(['git','diff','HEAD','--name-only','--','*.log','*.pyc','*.pid']).strip()
receipt={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'sourceSecretTextFiles':source['textFiles'],'componentsSecretTextFiles':components['textFiles'],'componentsHistoricalMatches':components['totalMatches'],'componentsAddedMatches':len(components['added']),'sourceSecretBinaryFiles':source['binaryFiles'],'historicalSourceMatches':source['totalMatches'],'addedSourceMatches':len(source['added']),'removedSourceMatches':len(source['removed']),'inheritedDocumentationLogPaths':len(a),'newDocumentationLogPaths':sorted(b-a),'removedDocumentationLogPaths':sorted(a-b),'documentationStatus':'FAILED_INHERITED_DEBT; no gate waiver or merge readiness'}
(out/'summary.json').write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf-8',newline='\n')
print(json.dumps(receipt))
