from pathlib import Path
import json,subprocess,datetime,hashlib,os,sys
root=Path.cwd();s=root/'.mcp-state/capa03';records=[]
previous=json.loads((s/'responsive-assembly-review.json').read_text(encoding='utf-8'))['before']
commands=[r['command'] for r in json.loads((s/'responsive-builds.json').read_text(encoding='utf-8'))]
commands += [r['command'] for r in json.loads((s/'responsive-test-builds.json').read_text(encoding='utf-8'))]
for i,command in enumerate(commands):
 start=datetime.datetime.now(datetime.timezone.utc).isoformat()
 with (s/f'cascade-build-{i}.txt').open('wb') as f:r=subprocess.run(command,stdout=f,stderr=subprocess.STDOUT)
 records.append({'command':command,'exit':r.returncode,'startedUtc':start,'finishedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'log':f'cascade-build-{i}.txt'})
 (s/'cascade-builds.json').write_text(json.dumps(records,indent=2)+'\n',encoding='utf-8')
 print(i,r.returncode,flush=True)
 if r.returncode:sys.exit(r.returncode)
after={p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in previous}
(s/'cascade-assembly-review.json').write_text(json.dumps({'before':previous,'after':after,'unchanged':after==previous},indent=2)+'\n',encoding='utf-8')
assert after==previous
for kind,filter_,count in [('Unit','FullyQualifiedName~CatalogSandboxContextTests.|FullyQualifiedName~CatalogAssetModeTests.|FullyQualifiedName~CapabilitiesSandboxContextTests.',60),('Components','FullyQualifiedName~CapabilitiesSandboxTests.',38)]:
 r=subprocess.run(['python',str(s/'run-selection.py'),'cascade-'+kind,'tests/'+kind+'/CanDoItAll.Tests.'+kind+'/CanDoItAll.Tests.'+kind+'.csproj',filter_,str(count)])
 if r.returncode:sys.exit(r.returncode)
env=os.environ.copy();env['CAPA_PLAYWRIGHT']=r'C:\Users\lucys\AppData\Local\npm-cache\_npx\e41f203b7505f1fb\node_modules\playwright'
r=subprocess.run(['node',str(s/'browser-sandbox.cjs'),str(s/'browser-responsive-cascade-green')],env=env)
sys.exit(r.returncode)
