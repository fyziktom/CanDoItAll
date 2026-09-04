# SB00 — MAF 1.20 Upgrade And Characterization

## Status

- `Completed`

## Objective

Upgrade the coherent MAF dependency family from 1.18 to 1.20, characterize all affected boundaries, and establish the actual SDK baseline before fixing CanDoItAll's application-owned tool outcome defects.

## Covered Inputs

- N05, N06, N07, N10; R01, R02, R06, R10, R12 and R13; F01–F05 plus the MAF assessment findings.

## Prerequisites

- Review bundle://analysis/03-maf-1-20-assessment.md and the sanitized 1.20 probe.
- Verify upstream versions remain 1.20.0 stable, 1.20.0-preview.260831.1 A2A/Hosting and MEAI 10.9.0.
- Confirm source baseline and current restored graph; do not infer package compatibility from release notes alone.

## Exact Source References

- `repo://src/MAF/MicrosoftAgentFramework.Packages.props`
- `repo://Directory.Build.props`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj`
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj`
- `repo://src/MAF/Memory/CanDoItAll.AgentFramework.Memory/CanDoItAll.AgentFramework.Memory.csproj`
- `repo://src/Modules/CanDoItAll.Modules.SchedulerPlanner/CanDoItAll.Modules.SchedulerPlanner.csproj`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafWorkflowStreamingRunDriver.cs`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafWorkflowTurnResultMapper.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafWorkflowTurnResultMapperTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/McpRuntimeContractsTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentA2AMetadataTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureAgentRuntimeToolRoundTripIntegrationTests.cs`

## Deliverables

- Stable MAF 1.20.0 and matching A2A/Hosting preview 1.20.0-preview.260831.1 pins.
- Aligned MEAI 10.9.0 direct references and Microsoft.Extensions 10.0.11 floor, with one clear version owner per family.
- Resolved dependency inventory proving no MAF 1.18, MEAI 10.8, unintended OpenAI 2.13+, or NU1605 downgrade remains.
- Compiling agent, workflow, MCP, A2A, memory, scheduler and hosting consumers with no suppressed upgrade error.
- Post-upgrade schema/binder/tool-result, workflow error/cancellation and provider serializer characterization.
- A concise asset tool description/example that names projectId and request, and places parentNodeKey and every content-source field, including sourceWorkspacePath, inside request.
- A schema-description conformance test for representative model-visible tools, without changing their wire contracts.

## Dependency Impact

- This is a critical foundation for SB01–SB06. Any schema, result, session, workflow-event or serializer delta must update their assumptions and reopen affected specifications.
- Directory.Build.props and shared package versions affect the whole repository. The production solution build is an SB00 gate; the stable aggregate is consolidated at the final SB06 frozen checkpoint.

## Validation Depth

- Proof tier: `Behavioral`.
- Test project/filter/expected cases: V00 in bundle://plan/validation-plan.md.
- Selection reason: package upgrades can compile while changing runtime message, schema, workflow, A2A or MCP behavior.
- Expected cases: 5 new compatibility cases; 3 workflow mapper; 9 A2A; selected 4 MCP; current 12 project-structure round-trip cases. Re-list all filters and update the bundle before execution if intentional tests change counts.
- Invalidation keys: MAF/MEAI/A2A/OpenAI/MCP version, generated schema, AIFunction binder, FunctionResultContent, AgentSession, workflow RunStatus/events, streaming, cancellation.
- Broad-gate decision: Required once at final frozen SB06 because root package versions and shared runtime contracts are named invalidation triggers. Do not run the full stable aggregate twice.
- Critical foundation surfaces: agent tool loop and workflow/MCP/A2A integrations.
- Portability static is mandatory because root/build and source-controlled package files change.

## Implementation Steps

1. Capture baseline resolved versions and run the existing 1.18 characterization before editing.
2. Change MAF stable/preview pins coherently. Introduce one root MEAI version property only if all six direct consumers use it; otherwise update every direct 10.8 reference explicitly. Raise MicrosoftExtensionsPackageVersion from 10.0.10 to 10.0.11.
3. Keep OpenAI at 2.12.x and OllamaSharp unchanged. Do not combine unrelated dependency upgrades.
4. Restore the product and test graphs. Treat NU1605, mixed 1.18/1.20 assets, unexpected transitive versions, vulnerabilities or compatibility warnings as failures.
5. Resolve source breaks with the smallest adapter changes in their current owners. Do not use NoWarn, binding redirects or copied legacy SDK types as a compatibility strategy.
6. Add the asset description/schema conformance test and concise nested-argument example. Preserve camelCase wire names; do not accept arbitrary snake_case aliases.
7. Run post-upgrade binder/schema/result characterization through native and OpenAI-compatible serializers. Confirm malformed input still needs SB01.
8. Run workflow hard-error/cancellation, MCP lifecycle/result, A2A and project tool round-trip filters plus full production solution build.
9. Run portability static and record the dependency/architecture diff. Pass the frozen SDK baseline to SB01.

## C# Architecture Impact

Only package/adaptation boundaries may change. Neutral Models/Core/Workbench contracts must not absorb SDK types. New routing APIs are not adopted without a separate demonstrated need.

## Boundary Ownership

MAF and MEAI SDK adaptation remains in AgentFramework.Maf and Workflows.MafAdapter. Hosting owns A2A integration; the existing MCP project owns MCP protocol clients. Application outcome policy remains SB01/SB02.

## Dependency Direction

No new project reference is expected. Package changes must preserve the architecture map. Directory.Build.props supplies a coherent Microsoft.Extensions floor; project files consume versions without reverse project dependencies.

## Pattern Decision

Use existing adapters and a centralized package version property where it removes repeated literal pins. Do not add a compatibility facade until a concrete changed API requires it.

## Testability Contract

Use actual 1.20 assemblies and serializers. Fake only external network endpoints. Exercise real AIFunction binding, workflow event mapping, MCP lifecycle and A2A card/tool factories; no version-string-only test can close behavior.

## Partial Class Policy

No new partial type. Package adaptation belongs in existing cohesive SDK adapters; do not move unchanged code between partial files to claim isolation.

## Architecture Proof Required

- Resolved package graph before/after, affected project list, source API diff and changed adapter callers.
- Build repo://CanDoItAll.slnx after restore.
- CodeAnalytics or explicit dependency review showing no new project/module cycle or SDK leakage.
- Review every package-related portability finding and final no-write enforcement.

## UI Composition Contract

N/A — this phase changes dependencies and backend adapters. SB05/SB06 own browser-visible behavior.

## Scope Exceptions

- MAF 1.20 does not repair F01/F02/F03; SB01–SB03 remain mandatory.
- RoutePersistingRoutingChatClient evaluation is documentation/characterization only unless a separate failure proves it necessary.
- Workflow failure mapping is regression scope. No current workflow integration with the reported ordinary agent run was found.

## Do Not Do

- Do not mark the incident fixed after package restore/build, adopt experimental routing speculatively, expose detailed exceptions, add snake_case fallback binding, update OpenAI beyond 2.12.x, or suppress downgrade/API warnings.

## Acceptance Checklist

- All intended MAF packages resolve to 1.20.0; A2A/Hosting resolve to the matching preview.
- MEAI resolves coherently to 10.9.0 and Microsoft.Extensions foundation dependencies meet 10.0.11 without NU1605.
- Generated project asset schema still requires projectId/request and preserves nested fields through both serializers.
- Captured malformed call still executes zero delegates and is recorded as evidence that SB01 is needed.
- Workflow Error/ExecutorFailed remains Failed; cancellation is not Completed.
- MCP and A2A focused regressions pass; complete production solution builds.
- No new SDK reference leaks into Core, Models, Workbench domain services or UI.
- Package update proof never claims the ordinary run-status defect fixed.

## Proof Required

- Before/after package inventory, restore logs, exact build and V00 discovery/execution results.
- Sanitized schema, malformed/corrected invocation and native/OpenAI transport captures.
- Workflow hard-error and cancellation state evidence; MCP/A2A regression results.
- Portability static scan/review/final enforcement and architecture checkpoint.
- Record official upstream version/release URLs and package closure without treating release prose as behavior proof.

## Browser Validation Logging

- N/A — no browser-visible markup change. Live/browser provider validation occurs in SB06.

## Progression Gate

- SB01 may start only when dependency restore, production solution build, V00 behavior and static/architecture gates pass.
- If 1.20 changes an assumed contract, update the owning subbundle and its expected evidence before progression.
- A passing upgrade does not waive any incident repair phase.

## Reopen Triggers

- Any MAF/MEAI/A2A/OpenAI/MCP package change or new upstream finding affecting argument schemas, function results, session state, workflow status/events, cancellation or streaming reopens SB00 and the impacted downstream phases.

## Suggested Agent Prompt

```text
Execute SB00 only after implementation is authorized. Upgrade the coherent MAF dependency family, characterize rather than assume behavior, keep SDK types at existing adapters, capture exact package and focused behavior proof, update downstream bundle assumptions, and stop if restore, build, static, workflow, MCP, A2A, schema or outcome gates fail.
```
