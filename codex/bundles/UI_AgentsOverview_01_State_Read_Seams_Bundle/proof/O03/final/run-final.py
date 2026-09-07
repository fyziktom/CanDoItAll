from pathlib import Path
import subprocess,json,sys,datetime
out=Path('.mcp-state/overview03/final')
for command in [['python',str(out/'build-final.py')],['python','.mcp-state/overview03/audit-graph.py','final']]:
 r=subprocess.run(command)
 if r.returncode:sys.exit(r.returncode)
selections=json.loads(Path('.mcp-state/overview03/owning-selections.json').read_text())
selections.append({'name':'o03-new-components','project':'tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj','selector':'FullyQualifiedName~OverviewSandboxTests','expected':26})
for selection in selections:
 selection['name']=selection['name'].replace('o03-','o03-final-',1)
 print(selection['name'],flush=True)
 r=subprocess.run(['python','.mcp-state/overview-execution/run-selection.py',selection['name'],selection['project'],selection['selector'],str(selection['expected'])])
 if r.returncode:sys.exit(r.returncode)
(out/'owning-selections.json').write_text(json.dumps(selections,indent=2)+'\n',encoding='utf-8')
