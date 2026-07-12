# Missing Standard And Plugin Executor Coverage

## Status

- `Completed`

## Objective

- Add high-value missing executor nodes and operations through the shared contribution/operation architecture, including plugins.

## Success Criteria

- `document.to-markdown`, `image.inspect`, and usage-aware `image.analyze` are runnable contributions.
- `storage.file` supports exact directory listing and spreadsheet supports preview; other existing operations are not fragmented into redundant nodes.
- `command.process` is implemented only with typed allow-listed recipes, approval, cancellation, and masked failures.
- Bundled plugin contributions use real defaults/schema/simulation and have execution/negative tests.

## Covered Inputs

- WF-EXEC-01 through WF-EXEC-04 and WF-PLUGIN-01.
- MAF file/MarkItDown/image/command tool inventory and plugin executor requirement.

## Prerequisites

- SB01 contribution registry and SB02 shared operation gates pass.
- Persisted executor ID/settings compatibility review is complete.

## Exact Source References

- `repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Core/BuiltInWorkflowExecutorDescriptors.cs`
- `repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace/WorkspaceFileWorkflowExecutor.cs`
- `repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents/SpreadsheetWorkflowExecutor.cs`
- `repo://src/plugins/Implementations/CanDoItAll.Plugin.Gmail/GmailWorkflowExecutor.cs`
- `repo://src/plugins/Implementations/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs`
- `repo://src/plugins/Implementations/CanDoItAll.Plugin.Docker/DockerWorkflowExecutors.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/PluginWorkflowExecutorBoundaryTests.cs`

## Deliverables

- Strongly typed settings/IDs/descriptors/adapters for document conversion and image inspect/analyze.
- Storage directory-listing and spreadsheet-preview enum operations.
- Safe command recipe executor or an explicit tested safety blocker that keeps the planned descriptor unavailable.
- Plugin contribution/manifest parity and direct Gmail/Office365/Docker executor tests.
- Catalog, invoker, preview/simulation, approval, timeout/cancellation, audit, and failure coverage.

## Dependency Impact

- SB05 consumes image/provider usage from executor results.
- SB06 must render every runnable contribution without ID-specific changes.
- Persisted definitions depend on stable IDs and settings JSON.

## Validation Depth

- `Critical semantic implementation` with positive execution, adversarial unsafe-input, real-DI parity, and anti-stub proof for every new runnable node.

## C# Architecture Impact

- Exercises the contribution and shared-operation extension points; any need to edit unrelated central switches reopens SB01/SB02.

## Boundary Ownership

- Executor adapters own settings/input/output mapping only. Operation services own behavior; invoker owns policy/audit.

## Dependency Direction

- Standard/plugin contributions depend inward on abstractions/operations. No executor references a UI module or runtime tool class.

## Pattern Decision

- Use contribution registry plus ports/adapters. Use a typed Strategy/Command recipe set for `command.process`, not raw command strings.

## Testability Contract

- Each adapter is directly unit-tested with a fake operation; real DI proves catalog/invoker parity; unsafe command/plugin cases prove no operation call.

## Partial Class Policy

- New executors are small non-partial sealed classes. Split by operation/service ownership, never by partial file.

## Architecture Proof Required

- Extension-point diff, no central executor-ID branch beyond stable ID declarations, real plugin descriptors, and no duplicate operation implementation.

## Implementation Steps

1. Add IDs/settings/descriptors and failing catalog/invoker tests.
2. Implement document conversion and image adapters over SB02 operations.
3. Add storage directory-list and spreadsheet preview operations.
4. Design typed command recipes; add rejection/approval tests before making planned descriptor runnable.
5. Migrate/validate bundled plugin contributions and add direct execution tests.
6. Run focused policy, audit, preview, catalog, and failure suites.

## Scope Exceptions

- Do not add one node per filesystem/spreadsheet/runtime tool.
- Spreadsheet function catalog remains editor assistance unless a real runtime workflow scenario is proven.

## Do Not Do

- Do not expose raw PowerShell/Python/shell text.
- Do not call MAF tools from executors.
- Do not mark a descriptor runnable without a resolvable implementation and tests.

## Acceptance Checklist

- Every new runnable ID has one contribution and implementation.
- Document/image/storage/spreadsheet behavior delegates to shared operations.
- Command recipes are typed and approval-aware or remain explicitly planned.
- Real plugin catalog defaults/simulation match implementations.
- Focused tests/build pass.

## Proof Required

- Per-capability failing-first, semantic positive, adversarial negative, and anti-stub transcripts.
- Real-DI catalog/invoker ID list and descriptor parity transcript.
- `bundle://proof/SB03/manifest.md` and `bundle://proof/SB03/semantic-invariants.md` during execution.

## Browser Validation Logging

- `N/A in SB03: UI discovery and editing are proven in SB06 after analytics dependencies are ready.`

## Progression Gate

- SB05/SB06 remain blocked until every runnable contribution has execution, failure, policy, parity, and build proof and unsafe command exposure is absent.

## Suggested Agent Prompt

```text
Implement SB03 only. Add the named high-value nodes through shared operations and the unified contribution seam. Treat command execution and plugin metadata as security boundaries, prove real DI parity, and do not create one node per tool function.
```
