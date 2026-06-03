# Current State Analysis

## Direct Coupling

Current MAF project reference:

```xml
<ProjectReference Include="..\CanDoItAll.Modules.Processes\CanDoItAll.Modules.Processes.csproj" />
```

Current process tool file:

```text
src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProcessTools.cs
```

This file directly imports:

```csharp
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
```

It resolves these process services from the runtime service provider:

```text
ProcessesService
ProcessTemplateCatalogService
ProcessTemplatePackLoader
ProcessTemplateProjectionService
ProcessTemplateMermaidExporter
```

It then builds the process tool surface directly inside the MAF adapter.

## MAF Capability Composition Hotspots

`MafAgentRuntime.Capabilities.cs` currently:

- calls `AttachInternalProcessToolsAsync(...)`;
- calls `CreateProcessToolBuilder()`;
- keeps `ProcessToolBuilder? ProcessToolBuilder` inside `RuntimeCapabilityComposition`;
- wraps process mutation tools through `WrapInternalProcessMutationTool(...)`.

The approval wrapper behavior must be preserved, but hard-coded knowledge of process tools must leave MAF.

## Existing Process Tool Surface

The current process tool surface includes 23 process tools:

- definitions list/editor/save/role-add/publish/delete/export/import
- runs list/detail/analytics/start
- step transition
- assignment resolve
- artifact record
- party and executor options
- template list/detail/mermaid/import/baseline-scenarios/live-run-profiles

These tools are classified in:

- `AgentToolInvocationPolicyMetadata`
- `ToolContractCatalog`
- `ToolCapabilityRegistry`

The move must preserve those names and policy classifications.

## Dispatcher Scope

Dispatcher files:

```text
src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService*.cs
```

Observed size:

```text
33 files
about 25,511 total lines
largest partial: ArtifactValidation.cs (~3,933 lines)
```

The dispatcher already contains domain-specific pressure points such as:

- `DotnetRunCleanup`
- `WebHostProof`
- `BrowserProof`
- governed required-tool inference involving `workspace_dotnet_*`
- implementation proof and artifact validation logic

This bundle intentionally avoids moving dispatcher logic. It only creates the dependency inversion seam needed for later process-core and driver work.

## Existing Test Surfaces Likely Affected

- `AgentRuntimeHardeningStaticRegressionTests`
- `AgentToolInvocationPolicyTests`
- `AgentFrameworkExecutionCapabilityFilteringIntegrationTests`
- `MafAgentRuntimeTests`
- `MafPackageBaselineReflectionTests`
- plugin capability tests that assert no direct MAF dependency
- process and Playwright audit proof tests that seed process definitions/runs
- component tests that transitively rely on app composition

## Immediate Risk

A naive migration can pass build while silently changing runtime behavior:

- process tools might not attach when Processes module is registered;
- process read tools might become approval-wrapped;
- process mutation tools might become approval-free;
- process DTO types might accidentally move into MAF-facing abstractions too early;
- service lifetime can change from scoped to singleton and capture scoped services incorrectly;
- MAF might still depend on Processes through a helper, test reference, or reflection.
