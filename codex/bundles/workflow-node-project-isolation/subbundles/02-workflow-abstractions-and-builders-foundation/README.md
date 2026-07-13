# SB02 - Workflow Abstractions And Builders Foundation

## Status

- `Completed`

## Objective

Create the workflow-owned abstractions, builders, factories, graph construction helpers, and test fixtures that later core, runtime, template, UI, and Workbench code can depend on without referencing MAF or Blazor modules.

## Success Criteria

- Workflow contract and builder projects exist and compile independently of MAF, UI, plugins, and persistence implementations.
- Workflow builders/factories remove repeated hand-built node, edge, port, input parameter, and executor-node setup from tests and template code.
- Base workflow failure diagnostic contracts exist and serialize compatibly with event payload usage.
- Public contracts remain strongly typed and preserve existing serialized model compatibility.
- Boundary tests prove forbidden references do not exist.

## Covered Inputs

- R03, R04, R05, R10, R12, R13, R14, R15, R17.
- Architect note that workflows need proper builders and factories.
- Architect note that work must be built base-up.

## Prerequisites

- SB01 inventory and project graph accepted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowInputParameterModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowIdJsonConverters.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.Runtime\WorkflowContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowCatalogContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.Templates\WorkflowTemplatePack.cs`
- `C:\repositories\CanDoItAll\tests`

## Deliverables

- `CanDoItAll.AgentFramework.Workflows.Abstractions` with stable workflow contracts that do not depend on implementation projects.
- `CanDoItAll.AgentFramework.Workflows.Builder` with graph, node, edge, port, input, and test-fixture builders.
- Strongly typed diagnostic envelope contracts for workflow failures, including failure kind, retryability, repair hint, redacted technical detail, and correlation id.
- Project reference updates limited to contract/builders consumers required for tests.
- Unit tests for builder output, compatibility serialization, and diagnostic envelope serialization.
- Boundary tests that reject references from abstractions/builders to MAF, UI, plugin implementation, or persistence implementation projects.

## Dependency Impact

- SB03, SB04, SB10, SB11, and SB12 all consume these contracts. If SB02 permits leaky abstractions or weak builders, later subbundles will duplicate construction logic and keep the old coupling under new project names.

## Validation Depth

- `Critical foundation`
- Unit, compatibility, and dependency-boundary tests.

## Implementation Steps

1. Create workflow abstraction and builder project skeletons using existing solution conventions.
2. Move only stable contracts and strongly typed helper shapes needed by workflow core, runtime, templates, and tests.
3. Define the minimal diagnostic envelope and value objects required by `architecture/04-failure-diagnostics-and-error-state-boundary.md`; keep it implementation-neutral.
4. Implement builders/factories for definitions, nodes, ports, edges, input parameters, executor nodes, deterministic fixtures, and failure-event fixture payloads.
5. Replace only the smallest necessary test fixture construction to prove the builders work.
6. Add serialization compatibility tests for representative existing workflow definitions and diagnostic payloads.
7. Add project-reference guard tests or architecture tests for dependency direction.
8. Update inventories, traceability, and execution report.

## Scope Exceptions

- Runtime manager, validators, stores, executor catalog, and MAF compiler/backend remain in their current projects until later subbundles.
- Do not convert UI template loading in this phase beyond test fixture proof.

## Do Not Do

- Do not add fallback conversion paths that silently swallow malformed definitions.
- Do not introduce interfaces with one trivial implementation unless they define an actual cross-project boundary or test seam.
- Do not move plugin abstractions here; plugin executor contracts are owned by SB08.
- Do not add diagnostics as loose dictionaries or magic-string category fields when a typed enum/value object can express the contract.

## Acceptance Checklist

- [x] New abstraction and builder projects compile.
- [x] Builders cover normal, branching, invalid, and executor-node fixture construction.
- [x] Diagnostic envelope contracts cover at least failure kind, retryability, repair hint, redacted technical detail, source context, and correlation id.
- [x] Existing workflow JSON fields and ids remain compatible.
- [x] Boundary tests prove no MAF/UI/plugin implementation dependency from workflow abstractions or builders.
- [x] Traceability records which workflow model contracts stayed in Models and which moved.

## Execution Notes

- Added `CanDoItAll.AgentFramework.Workflows.Abstractions` for implementation-neutral workflow diagnostics and workflow service boundary contracts.
- Added `CanDoItAll.AgentFramework.Workflows.Builder` for deterministic workflow, node, edge, port, input-parameter, executor-node, branching, invalid-fixture, and diagnostic fixture construction.
- Kept existing serialized workflow models, ids, input parameter descriptors, executor ids, and JSON converters in `CanDoItAll.AgentFramework.Models` to preserve compatibility; no existing model contract was moved in SB02.
- Added focused unit coverage in `tests/CanDoItAll.Tests.Unit/WorkflowAbstractionsBuilderTests.cs`.
- Used `--artifacts-path artifacts\codex-sb02-unit` for unit validation because `CanDoItAll.Tests.Support` references `CanDoItAll.Web`, and a live `CanDoItAll.Web` process was locking the default web output directory.

## Proof Required

- `proof/SB02/manifest.md` with changed file hashes, build/test transcripts, and dependency graph proof.
- `proof/SB02/semantic-invariants.md` with at least: serialized workflow compatibility, diagnostic payload compatibility, deterministic builder output, no hidden fallback, and no forbidden references.
- Semantic Adequacy Gate proof: shallow-pass trap, adversarial negative proof for invalid graph inputs, semantic positive proof for representative workflow construction, anti-stub audit.

## Browser Validation Logging

- `N/A`. This subbundle does not change browser-visible behavior.

## Progression Gate

- SB03 and SB04 cannot move services until SB02 contracts/builders compile, pass tests, and prove forbidden dependencies are absent.

## Suggested Agent Prompt

```text
Implement SB02 only. Build the workflow abstractions, diagnostic envelope contracts, and builder/factory foundation from the accepted project graph. Keep changes minimal, preserve serialization compatibility, add boundary, builder, and diagnostic serialization tests, and capture Semantic Adequacy Gate proof. Do not move runtime, executor, template, plugin, MAF, or UI implementation code in this subbundle.
```
