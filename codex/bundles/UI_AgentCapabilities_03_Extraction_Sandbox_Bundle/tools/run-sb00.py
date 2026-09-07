from pathlib import Path
import json,subprocess,datetime,sys,xml.etree.ElementTree as E
out=Path('.mcp-state/capa03'); ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
owners={'unit':['AgentCapabilitiesSessionTests','CatalogSandboxContextTests','CatalogAssetModeTests'],'components':['AgentCapabilitiesSurfaceTests','AgentCapabilitiesHostTests','AgentCapabilitiesReadLifecycleTests','AgentCapabilityListTests','AgentEditorCapabilityCompositionTests','AgentDetailsDialogCapabilityTests','AgentsHomePageTests']}
selections=[]
for kind,classes in owners.items():
 project='tests/'+kind.capitalize()+'/CanDoItAll.Tests.'+kind.capitalize()+'/CanDoItAll.Tests.'+kind.capitalize()+'.csproj'
 selector='|'.join('FullyQualifiedName~'+c+'.' for c in classes)
 if kind=='components':selector+='|(FullyQualifiedName~AgentPanelSelectionFailClosedTests.&FullyQualifiedName~Capabilities_panel_)'
 base=['dotnet','test',project,'-c','Release','--no-build','--no-restore','--filter',selector]
 with (out/('sb00-'+kind+'-discovery.txt')).open('wb') as log:r=subprocess.run(base+['--list-tests'],stdout=log,stderr=subprocess.STDOUT)
 names=[s.strip() for s in (out/('sb00-'+kind+'-discovery.txt')).read_text(encoding='utf-8-sig').splitlines() if s.strip().startswith('CanDoItAll.Tests.')]
 assert r.returncode==0 and names
 assert all(any('.'+c+'.' in n for n in names) for c in classes)
 selections.append({'kind':kind,'command':base,'expected':len(names),'names':names,'discoveredUtc':datetime.datetime.now(datetime.timezone.utc).isoformat()})
(out/'sb00-selections.json').write_text(json.dumps(selections,indent=2)+'\n',encoding='utf-8')
print('Frozen',[(s['kind'],s['expected']) for s in selections],flush=True)
results=[]
for selection in selections:
 kind=selection['kind'];command=selection['command']+['--logger','trx;LogFileName=sb00-'+kind+'.trx','--results-directory',str(out)];started=datetime.datetime.now(datetime.timezone.utc).isoformat()
 with (out/('sb00-'+kind+'-result.txt')).open('wb') as log:r=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT)
 t=E.parse(out/('sb00-'+kind+'.trx'));counts=t.find('.//t:Counters',ns).attrib
 result={'kind':kind,'command':command,'exit':r.returncode,'startedUtc':started,'finishedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'counters':counts,'nonPass':[{k:v for k,v in x.attrib.items() if k in ['testName','outcome']} for x in t.findall('.//t:UnitTestResult',ns) if x.attrib['outcome']!='Passed']};results.append(result)
 (out/'sb00-tests.json').write_text(json.dumps(results,indent=2)+'\n',encoding='utf-8');print(kind,counts,flush=True)
 assert r.returncode==0 and int(counts['executed'])==selection['expected'] and counts['notExecuted']=='0'
