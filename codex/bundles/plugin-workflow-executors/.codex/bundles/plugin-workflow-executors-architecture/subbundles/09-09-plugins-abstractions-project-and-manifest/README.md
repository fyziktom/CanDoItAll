# Plugins Abstractions Project And Manifest

## Status

- `Ready`

## Objective

- Create plugin abstractions project and manifest/capability contracts.

## Success Criteria

- A dedicated plugin abstractions project exists with stable ids, descriptors, manifest, capability, settings, connection, and executor contracts.
- Contracts avoid raw IServiceProvider exposure.
- Contracts support future OAuth2 and shop package metadata without forcing implementations now.
- Build/tests prove duplicate id/capability semantics.

## Covered Inputs

- `R001`
- `R003`
- `R004`
- `R008`
- `R010`
- `R014`
- `R015`
- `R027`
- `R030`
- `F001`
- `F005`
- `F011`
- `F012`

## Prerequisites

- `SB08`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowExecutorModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Connectors\ConnectorManifest.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\ModuleAssemblies.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`

## Deliverables

- New `src/CanDoItAll.Plugins.Abstractions` project or approved equivalent.
- Plugin id/version/source/trust/capability models.
- Plugin workflow executor abstractions.
- Plugin settings/renderer descriptor contracts.
- Plugin connection/auth/OAuth extension contracts.
- Unit tests for equality, duplicate detection helpers, and manifest validation.

## Dependency Impact

- Shop, OAuth2, final proof, and future SaaS plugin bundles depend on this MVP being coherent and bounded.

## Validation Depth

- `Plugin MVP implementation`

## Implementation Steps

1. Create the abstractions project with minimal references, preferably SharedKernel and AgentFramework.Models only if needed.
2. Define strongly typed plugin/package/connection ids.
3. Define PluginDescriptor/manifest with version, vendor, source, trust, capabilities, settings, connections, and workflow executors.
4. Define plugin workflow executor contract that can be bridged to IWorkflowExecutor without plugins receiving arbitrary IServiceProvider.
5. Define capability context interfaces for secrets/files/storage/project/http/oauth/events.
6. Define renderer descriptor and settings descriptor contracts.
7. Add manifest validation for duplicate plugin/executor/renderer keys and unsupported capabilities.
8. Add project references only where required and update solution.

## Scope Exceptions

- No Plugins module, persistence, UI, or API yet.
- No dynamic loading.

## Do Not Do

- Do not reference implementation modules from abstractions.
- Do not expose IServiceProvider or raw vault/storage driver as generic plugin context.
- Do not put Blazor component implementation in the abstractions project.

## Acceptance Checklist

- [ ] A dedicated plugin abstractions project exists with stable ids, descriptors, manifest, capability, settings, connection, and executor contracts.
- [ ] Contracts avoid raw IServiceProvider exposure.
- [ ] Contracts support future OAuth2 and shop package metadata without forcing implementations now.
- [ ] Build/tests prove duplicate id/capability semantics.

## Proof Required

- `dotnet build src\CanDoItAll.Plugins.Abstractions\CanDoItAll.Plugins.Abstractions.csproj`
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "PluginManifest|PluginAbstractions"`

## Browser Validation Logging

- N/A.

## Progression Gate

- Passed only when plugin contracts are separate, stable, and implementation-module-free.

## Suggested Agent Prompt

```text
Implement SB09 only.

Work outcome-first:
- Read this subbundle README, the root README, and reviews/01-execution-report.md.
- Verify prerequisites and exact source references before editing.
- Preserve the listed scope boundaries.
- Make the smallest correct change set.
- Capture required proof.
- Update reviews/01-execution-report.md.
- Stop if the progression gate cannot honestly pass.
```
