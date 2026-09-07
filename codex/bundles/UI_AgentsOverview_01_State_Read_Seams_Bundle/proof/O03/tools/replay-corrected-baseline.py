from pathlib import Path
import json,hashlib,subprocess,datetime,sys
root=Path.cwd().resolve();state=root/'.mcp-state/overview03';out=root/'.mcp-state/overview03-corrected';out.mkdir(exist_ok=True)
sha=lambda b:hashlib.sha256(b).hexdigest()
initial=json.loads((state/'planned-edits.json').read_text());movement=json.loads((state/'moves.json').read_text())['moves']
assert not (out/'replay-receipt.json').exists(), 'A corrected baseline receipt already exists.'
post={};pre={};relocations=[]
for item in movement:
 source=root/item['to'];target=root/item['from'];assert source.is_file() and not target.exists()
 assert source.resolve().is_relative_to(root) and target.resolve().is_relative_to(root)
 post[source]=source.read_bytes();data=post[source]
 if source.name=='AgentUsageDisplay.cs':
  data=data.replace(b'CanDoItAll.AgentFramework.UI.Overview',b'CanDoItAll.Modules.AgentFramework.Pages.Components').replace(b'public static class AgentUsageDisplay',b'internal static class AgentUsageDisplay')
 elif source.name=='ProviderUsageConsumerList.razor':
  prefix=b'@namespace CanDoItAll.AgentFramework.UI.Overview\n';assert data.startswith(prefix);data=data[len(prefix):]
 else:data=data.replace(b'CanDoItAll.AgentFramework.UI.Overview',b'CanDoItAll.Modules.AgentFramework.Pages.Components.Overview')
 comparison=data.replace(b'ColumnTemplate2Xl="minmax(20rem,0.95fr)',b'ColumnTemplateXl="minmax(20rem,0.95fr)') if source.name=='AgentsOverviewSurface.razor' else data
 assert sha(comparison)==item['beforeSha256'], 'The corrected pre-owner must differ only by the proven breakpoint correction: '+str(source)
 pre[target]=data;relocations.append((source,target))
for relative in ['src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor.cs','src/Modules/CanDoItAll.Modules.AgentFramework/_Imports.razor']:
 p=root/relative;post[p]=p.read_bytes();pre[p]=post[p].replace(b'CanDoItAll.AgentFramework.UI.Overview',b'CanDoItAll.Modules.AgentFramework.Pages.Components.Overview')
p=root/'src/UI/CanDoItAll.AgentFramework.UI/_Imports.razor';post[p]=p.read_bytes()
footer=b'\n@using CanDoItAll.AgentFramework.UI.Overview\n@using CanDoItAll.AgentFramework.Usage\n@using CanDoItAll.Components.Charts\n';assert post[p].endswith(footer);pre[p]=post[p][:-len(footer)]
p=root/'src/UI/CanDoItAll.AgentFramework.UI/CanDoItAll.AgentFramework.UI.csproj';post[p]=p.read_bytes();pre[p]=post[p].replace(b'    <PackageReference Include="CanDoItAll.Components.Charts" Version="$(CanDoItAllComponentsPackageVersion)" />\n',b'').replace(b'    <ProjectReference Include="../../MAF/Common/CanDoItAll.AgentFramework.Usage/CanDoItAll.AgentFramework.Usage.csproj" />\n',b'')
for p in [root/'src/UI/CanDoItAll.AgentFramework.UI/_Imports.razor',root/'src/UI/CanDoItAll.AgentFramework.UI/CanDoItAll.AgentFramework.UI.csproj']:
 expected=next(row['sha256'] for row in initial['sourceInventory'] if root/row['path']==p)
 candidates=[pre[p],pre[p].replace(b'\r\n',b'\n').replace(b'\n',b'\r\n')]
 pre[p]=next((data for data in candidates if sha(data)==expected),None)
 assert pre[p] is not None, 'Exact original UI project/import bytes required.'
assets=[root/'src/App/CanDoItAll.Web/wwwroot/css/output.css',root/'src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/wwwroot/css/catalog-fast.css']
postAssets={p:p.read_bytes() for p in assets}
if '--dry-run' in sys.argv:
 print('Validated exact pre-owner reconstruction and post-owner restoration for',len(post),'source paths.');sys.exit(0)
backup=out/'post-owner-backup'
for p,data in post.items():
 target=backup/p.relative_to(root);target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
receipt={'startedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'reason':'1280px long-content browser proved the inherited xl four-column bar plot collapsed. Corrected renderer uses existing 2xl breakpoint. Repeat pre-move baseline rather than compare changed source to stale measurements.','status':'RUNNING','postOwnerRestored':False,'originalResultsRetained':True,'commands':[]}
def command(args,name):
 with (out/name).open('w',encoding='utf-8') as log:r=subprocess.run(args,stdout=log,stderr=subprocess.STDOUT)
 receipt['commands'].append({'command':args,'exit':r.returncode,'utc':datetime.datetime.now(datetime.timezone.utc).isoformat()})
 print(name,r.returncode,flush=True)
 if r.returncode:raise RuntimeError('Command failed: '+name)
try:
 for source,target in relocations:
  target.parent.mkdir(parents=True,exist_ok=True);source.rename(target)
 for p,data in pre.items():p.write_bytes(data)
 (out/'pre-owner-inventory.json').write_text(json.dumps([{'path':str(p.relative_to(root)),'sha256':sha(data)} for p,data in pre.items()],indent=2))
 command(['node','Tailwind/node_modules/@tailwindcss/cli/dist/index.mjs','-i','Tailwind/input.css','-o','src/App/CanDoItAll.Web/wwwroot/css/output.css'],'pre-assets.txt')
 command(['dotnet','build','src/App/CanDoItAll.Web/CanDoItAll.Web.csproj','-c','Release','/m:1','-v:q'],'pre-owner-web-build.txt')
 command(['python',str(state/'audit-graph.py'),'corrected-pre-owner'],'pre-owner-graph.txt')
 plan=dict(initial);plan['status']='Frozen corrected pre-owner protocol v2; only proven Grid xl-to-2xl correction relative to original renderer';plan['edits']=[dict(e) for e in initial['edits']]
 for edit in plan['edits']:edit['preSha256']=sha((root/edit['prePath']).read_bytes())
 plan['sourceInventory']=[{'path':str(p.relative_to(root)).replace('\\','/'),'sha256':sha(p.read_bytes()),'bytes':p.stat().st_size} for p in sorted(set(pre)|set(assets))]
 plan['harnessSha256']=sha((state/'direct-watch.cjs').read_bytes());(out/'direct-watch.cjs').write_bytes((state/'direct-watch.cjs').read_bytes())
 (out/'planned-edits.json').write_text(json.dumps(plan,indent=2)+'\n',encoding='utf-8')
 restore=out/'restoration';restore.mkdir(exist_ok=True)
 for name,p in zip(['production.css','fast.css'],assets):(restore/name).write_bytes(p.read_bytes())
 for suffix in ['.razor','.razor.cs','.razor.css']:
  p=root/('src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/Overview/AgentsOverviewSurface'+suffix);(restore/p.name).write_bytes(p.read_bytes())
 runner=(state/'run-pre.py').read_text().replace("out=Path('.mcp-state/overview03')","out=Path('.mcp-state/overview03-corrected')")
 (out/'run-pre.py').write_text(runner,encoding='utf-8',newline='\n')
 command(['python',str(out/'run-pre.py')],'measurement-driver.txt')
 samples=[];cold=[]
 for d in (out/'measurements').glob('pre-fullapp-*'):
  rows=[json.loads(line) for line in (d/'ledger.jsonl').read_text(encoding='utf-8').splitlines()]
  assert any(r['kind']=='complete' for r in rows)
  if '-cold-' in d.name:cold.extend(r['elapsedMs'] for r in rows if r['kind']=='interactive-ready')
  samples.extend(r for r in rows if r['kind']=='sample')
 assert len(cold)==3 and len(samples)==54 and all(r['success'] for r in samples)
 for p,data in pre.items():assert p.read_bytes()==data, 'Pre-owner probe not restored: '+str(p)
 for name,p in zip(['production.css','fast.css'],assets):assert p.read_bytes()==(restore/name).read_bytes()
 receipt['status']='VALID CORRECTED PRE-MOVE BASELINE';receipt['coldMs']=cold;receipt['warmObservations']=len(samples);receipt['failures']=0
except BaseException as error:
 receipt['status']='FAILED';receipt['failure']=str(error);raise
finally:
 relocatedTargets={target for _,target in relocations}
 for p,data in pre.items():
  if p not in relocatedTargets:assert p.read_bytes() in (data,post.get(p)), 'Concurrent source edit; refusing restoration: '+str(p)
 for source,target in relocations:
  if target.exists():
   assert target.read_bytes()==pre[target], 'Concurrent edit; do not overwrite: '+str(target)
   assert not source.exists();target.rename(source)
 for p,data in post.items():p.write_bytes(data)
 for p,data in postAssets.items():p.write_bytes(data)
 assert all(p.read_bytes()==data for p,data in post.items())
 receipt['postOwnerRestored']=True;receipt['finishedUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
 (out/'replay-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf-8')
 print(json.dumps({k:v for k,v in receipt.items() if k!='commands'}),flush=True)
