import pathlib, subprocess, json, sys
sys.stdout.reconfigure(encoding='utf-8')
root=pathlib.Path.cwd(); out=root/'.mcp-state/capa02g'; bundle=root/'codex/bundles/UI_AgentCapabilities_02G_PreExtraction_Correctness_Bundle'
project='tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj'
f='FullyQualifiedName~CapabilityCorrectnessIntegrationTests.Capability_deletion_advances_only_affected_agent_revisions_and_satisfies_attachment_recovery|FullyQualifiedName~CapabilityCorrectnessIntegrationTests.Canonical_attachment_cleanup_advances_revision_once_and_recovery_observes_the_postcondition'
r=subprocess.run([sys.executable,str(out/'run-selection.py'),'green-additional-revision',project,f,'2'])
if r.returncode: sys.exit(r.returncode)
sets=json.loads((bundle/'proof/owning-selections.json').read_text(encoding='utf-8'))
extra={'owning-unit':'FullyQualifiedName~CapabilityMigrationCleanupGuardTests|FullyQualifiedName~CapabilityTemplateSeedHardeningCheckpointTests','owning-integration':'FullyQualifiedName~AgentFrameworkWorkspaceSeedIntegrationTests|FullyQualifiedName~AgentTeamCatalogIntegrationTests'}
for s in sets:
 s['name']=s['name'].replace('owning','final-owning'); key=s['name'].replace('final-owning','owning')
 if key in extra: s['filter']+='|'+extra[key]
 cmd=['dotnet','test',s['project'],'-c','Release','--no-build','--no-restore','--filter',s['filter'],'--list-tests']
 p=subprocess.run(cmd,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
 output=p.stdout.decode('utf-8',errors='replace'); (out/(s['name']+'-frozen-discovery.txt')).write_text(output,encoding='utf-8')
 s['frozenNames']=[l.strip() for l in output.splitlines() if l.strip().startswith('CanDoItAll.Tests.')];s['expected']=len(s['frozenNames']);s['discoveryCommand']=cmd
 if p.returncode or not s['expected']: raise RuntimeError('Discovery failed '+s['name'])
 print(s['name'],s['expected'],flush=True)
(bundle/'proof/final-owning-selections.json').write_text(json.dumps(sets,indent=2)+'\n',encoding='utf-8')
for s in sets:
 p=subprocess.run([sys.executable,str(out/'run-selection.py'),s['name'],s['project'],s['filter'],str(s['expected'])])
 if p.returncode: sys.exit(p.returncode)
