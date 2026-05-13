# Plugin Readiness Source Audit And Decision Gate

## Status

- `Ready`

## Objective

- Audit current code, confirm assumptions, update source map/risk register before edits.

## Success Criteria

- Source references in inventories/01-source-map.md still resolve or are corrected.
- Readiness conclusion is re-confirmed or updated with explicit changes.
- Execution report records the audited source snapshot and any source drift.
- No implementation work starts until the audit is complete.

## Covered Inputs

- `R001`
- `R002`
- `R003`
- `R004`
- `R005`
- `R006`
- `R007`
- `R008`
- `R010`
- `R011`
- `R012`
- `R013`
- `R017`
- `R018`
- `R023`
- `R026`
- `F001`
- `F002`
- `F003`
- `F004`
- `F005`
- `F006`
- `F010`
- `F011`
- `F013`
- `F014`
- `F015`

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowExecutorModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowDefinitionValidator.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\BuiltInWorkflowExecutorDescriptors.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\BuiltInWorkflowExecutorServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Services\AgentFrameworkModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Hosting\AgentFrameworkServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\WorkflowsApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowExecutorCanvasCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecurityModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecretRuntimeResolver.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecretVaults.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecurityModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Storage\Abstractions\StorageContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Files\WorkspaceFileContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Files\WorkspaceFileService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\ProjectStructureWorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Connectors\ConnectorManifest.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Connectors\ConnectorConfigState.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\ConnectorConfigFieldEditor.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\ModuleAssemblies.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Composition\ShellNavigation.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ApiEndpointRouteBuilderExtensions.cs`

## Deliverables

- Updated source map if paths drifted.
- Updated analysis/readiness notes if code changed.
- Updated risk register if new blockers are found.
- Execution report entry proving the audit.

## Dependency Impact

- Later plugin module, workflow bridge, settings UI, and shop work depend on this foundation. Weak proof here causes duplication, secret leakage, or unstable plugin boundaries later.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Open the bundle README and current source map.
2. Verify each exact source reference still exists.
3. Re-read workflow executor contracts, descriptor models, validator, descriptor factory, DI registration, API endpoint, and canvas UI.
4. Re-read secret vault/runtime resolver and secret UI components.
5. Re-read connector schema/state/field editor and current settings page.
6. Re-read storage/workspace/project-structure access points.
7. Confirm whether the readiness decision still holds.
8. Update source map, analysis, risks, and execution report only if drift is found.

## Scope Exceptions

- No code implementation in this subbundle unless source paths in the bundle need correction.

## Do Not Do

- Do not create plugin abstractions or plugin module.
- Do not change workflow executor contracts.
- Do not change UI.
- Do not assume previous chat memory is correct without source verification.

## Acceptance Checklist

- [ ] Source references in inventories/01-source-map.md still resolve or are corrected.
- [ ] Readiness conclusion is re-confirmed or updated with explicit changes.
- [ ] Execution report records the audited source snapshot and any source drift.
- [ ] No implementation work starts until the audit is complete.

## Proof Required

- Record inspected files in reviews/01-execution-report.md.
- If paths changed, include a concise before/after source map diff.
- No build is required unless source drift forces a bundle correction.

## Browser Validation Logging

- N/A. Source audit only.

## Progression Gate

- Passed only when the execution report says whether the bundle assumptions still match current source.

## Suggested Agent Prompt

```text
Implement SB01 only.

Work outcome-first:
- Read this subbundle README, the root README, and reviews/01-execution-report.md.
- Verify prerequisites and exact source references before editing.
- Preserve the listed scope boundaries.
- Make the smallest correct change set.
- Capture required proof.
- Update reviews/01-execution-report.md.
- Stop if the progression gate cannot honestly pass.
```
