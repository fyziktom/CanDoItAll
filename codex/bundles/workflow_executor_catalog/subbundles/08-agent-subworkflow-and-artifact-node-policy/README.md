# 08-agent-subworkflow-and-artifact-node-policy

## Status

- Status: `Completed`

## Closure Notes

- Added catalog-aware validation options so template loading can validate executor registration/schema without rejecting unavailable local plugin setup.
- Active unsupported helper node kinds now fail validation instead of silently passing through runtime execution.
- Added descriptor-source catalog composition so plugin executor descriptors can appear in the catalog without eagerly constructing executor implementations.
- Proof manifest: `bundle://proof/SB08/manifest.md`
- Semantic invariants: `bundle://proof/SB08/semantic-invariants.md`

## Objective

Resolve pass-through ambiguity for helper node kinds so active workflows do not silently ignore unsupported behavior.

## Covered Inputs

- RN02: Helper nodes users need must be implemented, blocked, or clearly visual-only.
- RN05: Do not overbuild durable production runtime while claiming unsupported active behavior.
- R8: Non-executor helper node kinds must be implemented, converted to executor-backed nodes, or blocked from active publish/run.
- R12: Durable backend honesty must remain stable.

## Prerequisites

- SB01 closure gate passed.
- SB06 closure gate passed for delay and approval helper semantics.
- SB07 closure gate passed if artifact/document node policy depends on ingestion outputs.
- All current `WorkflowNodeKind` values are audited.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowDefinitionValidator.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasModels.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowFoundationTests.cs`
- `repo://tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs`

## Scope

- Audit Start, LlmCall, Triage, StrictLogic, Executor, Artifact, HumanInput, AgentStep, Subworkflow, and End.
- Define each node kind as executable, visual-only, executor-backed, or validation-blocked.
- Block active unsupported Artifact, AgentStep, and Subworkflow nodes unless safe runtime bridges already exist.
- Preserve pass-through only where explicitly documented as visual-only or structural.
- Update validation and UI labels/messages to explain the required action.

## Dependency Impact

- SB09 template/UI authoring must reflect honest node semantics.
- SB10 final regression depends on active unsupported helper nodes failing before runtime pass-through.

## Validation Depth

- Unit tests proving active unsupported helper nodes cannot publish/run silently.
- Tests or source assertions proving explicitly visual-only nodes are allowed only in visual contexts.
- Component tests if UI labels or disabled states change.
- Critical proof manifest because this phase prevents semantic drift across all authored workflows.

## Implementation Steps

1. Inventory every `WorkflowNodeKind` and current compiler behavior.
2. Add validation rules for unsupported active helper nodes.
3. Implement or map safe node kinds to existing executor semantics only where fully tested.
4. Update UI labels and warnings if authoring surfaces change.
5. Add positive and negative tests for node-kind policy.

## Do Not Do

- Do not let active unsupported node kinds return input silently.
- Do not implement subworkflow recursion without recursion limits and runtime bridge proof.
- Do not invent an agent-step runtime if no safe invoker exists.
- Do not hide unsupported behavior behind generic validation errors.

## Acceptance Checklist

- Every `WorkflowNodeKind` has documented executable, visual-only, or blocked semantics.
- Active unsupported helper nodes fail validation before publish/run.
- Pass-through behavior exists only where explicitly documented and tested.
- UI labels and validation messages guide users to supported executor-backed alternatives.

## Proof Required

- `bundle://proof/SB08/manifest.md`
- `bundle://proof/SB08/semantic-invariants.md`
- Passing validator/compiler tests for helper node policy.
- Negative proof for active unsupported AgentStep, Subworkflow, and Artifact behavior.
- Changed-file hashes, source assertions, anti-stub audit, and component proof if UI changed.

## Browser Validation Logging

- Required if workflow authoring UI labels, disabled states, or validation display changes. Record route `agents/workflows`, viewport, actions, screenshots, and result.

## Progression Gate

- Continue to SB09 only after unsupported active helper nodes are blocked or implemented and pass-through semantics are explicitly limited.

## Suggested Agent Prompt

Use SB08 to make helper node semantics honest. Audit every node kind, block unsupported active nodes before runtime, and update UI messaging only where the authoring surface needs it.
