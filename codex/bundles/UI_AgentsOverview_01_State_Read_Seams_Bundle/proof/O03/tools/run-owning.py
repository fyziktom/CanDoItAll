from pathlib import Path
import json,subprocess,sys,datetime
out=Path('.mcp-state/overview03'); selections=[]
for kind in ['Unit','Components','Integration']:
 prior=json.loads(Path(f'.mcp-state/overview-execution/o02-final-CanDoItAll.Tests.{kind}-summary.json').read_text())
 args=prior['commands'][0]['command'];selector=args[args.index('--filter')+1]
 selections.append({'name':'o03-owning-'+kind.lower(),'project':f'tests/{kind}/CanDoItAll.Tests.{kind}/CanDoItAll.Tests.{kind}.csproj','selector':selector,'expected':prior['expected']})
selections.insert(0,{'name':'o03-new-unit-r2','project':'tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj','selector':'FullyQualifiedName~OverviewSandboxContextTests','expected':28})
for kind,classes in [('Unit',['CapabilitiesSandboxContextTests','CatalogSandboxContextTests','CatalogAssetModeTests']),('Components',['AgentCatalogPanelTests','AgentCatalogBoundaryTests','AgentCapabilitiesSurfaceTests','AgentAvatarRenderingTests','AgentAvatarActionButtonTests','CapabilitiesSandboxTests'])]:
 project=f'tests/{kind}/CanDoItAll.Tests.{kind}/CanDoItAll.Tests.{kind}.csproj';selector='|'.join('FullyQualifiedName~'+name for name in classes)
 command=['dotnet','test',project,'-c','Release','--no-build','--no-restore','--filter',selector,'--list-tests']
 r=subprocess.run(command,capture_output=True,text=True,encoding='utf-8');assert r.returncode==0
 names=[line.strip() for line in r.stdout.splitlines() if line.strip().startswith('CanDoItAll.')]
 assert all(any('.'+name+'.' in item for item in names) for name in classes)
 (out/f'compatibility-{kind}-frozen.json').write_text(json.dumps({'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'command':command,'names':names},indent=2)+'\n',encoding='utf-8')
 selections.append({'name':'o03-compatibility-'+kind.lower(),'project':project,'selector':selector,'expected':len(names)})
(out/'owning-selections.json').write_text(json.dumps(selections,indent=2)+'\n',encoding='utf-8')
for selection in selections:
 print(selection['name'],selection['expected'],flush=True)
 r=subprocess.run(['python','.mcp-state/overview-execution/run-selection.py',selection['name'],selection['project'],selection['selector'],str(selection['expected'])])
 if r.returncode:sys.exit(r.returncode)
