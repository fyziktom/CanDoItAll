# Propagate workflow project scope to agent context

## Status

- `Completed`

## Objective

- Ensure workflow LLM components pass project-structure project scope into MAF context contributors while preserving strict Cognitive Memory failures for missing scope and recall outages.

## Success Criteria

- `ContextWorkspaceScope` is populated from workflow JSON `projectId` or `project.id`.
- MAF context contribution policy uses the explicit context scope override.
- Missing project scope still fails governed Cognitive Memory context.
- Empty context packs skip explicitly with trace metadata.

## Covered Inputs

- User log failure: `Cognitive Memory context requires a project scope`.
- Requirement R1: project scope reaches MAF contributors.
- Requirement R2: missing project scope still fails.
- Requirement R3: empty memory does not fail payload-only workflow.

## Prerequisites

- none

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Models/Conversations/ConversationModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs`
- `repo://tests/CanDoItAll.Tests.Unit/AgentContextContributionTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`

## Deliverables

- Optional `AgentRuntimeExecutionOptions.ContextWorkspaceScope`.
- MAF runtime core capability-state builder accepting explicit context scope.
- Workflow LLM execution options derived from workflow payload project id.
- Cognitive Memory empty context packs treated as skipped context, not an outage.
- Focused unit coverage.

## Dependency Impact

- SB02 depends on this because the live Office365 workflow cannot safely reach the LLM summary without project-scoped context contribution.

## Validation Depth

- Critical foundation with unit proof and negative-governance checks.

## Implementation Steps

1. Add context workspace scope to runtime execution options.
2. Route the override into MAF context contribution policy.
3. Resolve workflow project id from payload JSON in the LLM invoker.
4. Change only empty Cognitive Memory context packs to skipped contribution.
5. Add unit tests covering positive, negative, and empty-memory paths.

## Scope Exceptions

- No UI changes.
- No Office365 executor changes.
- No project-structure lease behavior changes.

## Do Not Do

- Do not suppress all Cognitive Memory failures.
- Do not parse project scope from arbitrary prompt text for workflow execution.
- Do not introduce a fallback organization or sandbox scope for missing project ids.

## Acceptance Checklist

- `MafWorkflowLlmComponentInvokerPassesProjectScopeFromWorkflowPayload` passes.
- `Maf_runtime_uses_context_workspace_scope_override_for_contributors` passes.
- `Cognitive_memory_contributor_fails_process_automation_when_project_scope_is_missing` passes.
- `Cognitive_memory_contributor_fails_process_automation_when_required_memory_is_unavailable` passes.
- `Cognitive_memory_contributor_skips_empty_context_pack_for_process_automation` passes.

## Proof Required

- `bundle://proof/SB01/transcripts/unit-tests.txt`
- `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB01/semantic-invariants.json`
- `bundle://proof/SB01/manifest.md`

## Browser Validation Logging

- N/A: backend runtime and API workflow changes only.

## Progression Gate

- SB02 may proceed only after unit tests prove project scope override, missing-scope failure, recall-outage failure, and empty-context skip behavior.

## Suggested Agent Prompt

```text
Implement SB01 only. Preserve governed Cognitive Memory failures for missing project scope and real recall outages. Add project-scope propagation from workflow JSON to MAF context contributors, then prove it with focused unit tests.
```
