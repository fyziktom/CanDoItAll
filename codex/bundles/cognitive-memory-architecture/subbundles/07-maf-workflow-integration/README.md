# 07 MAF Workflow Integration

## Status

- Ready after recall, consolidation, and prerequisite MAF boundary.

## Objective

- Expose Cognitive Memory to MAF agents and workflow executors through extension contracts, tools, and context packs.

## Covered Inputs

- Requirements FR-011, FR-017, FR-018, FR-020, and NFR-013.
- MAF integration architecture and prerequisite boundary decision.

## Prerequisites

- `00-prerequisite-boundary-gate` must be closed.
- `05-recall-orchestrator` must provide traceable context packs.
- `06-consolidation-engine` must define reflection and run-memory behavior.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.Context.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Services\AgentFrameworkModuleServiceCollectionExtensions.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture\architecture\08-maf-workflow-agent-integration.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture\contracts\csharp\MafIntegrationContracts.cs

## Deliverables

- MAF context contributor adapter.
- Memory recall tools and workflow executors.
- Working memory lifecycle for agent/workflow runs.
- Reflection hooks that feed consolidation without direct table writes.

## Dependency Impact

- MAF consumes memory context; it does not own memory policy.
- Workflow executors call application services with authorization and trace context.
- Existing workspace memory remains compatibility fallback.

## Validation Depth

- Unit tests for context contributor selection and policy checks.
- Integration tests for workflow executor registration and recall tool output.

## Implementation Steps

- Register Cognitive Memory context contributor through the new MAF boundary.
- Add workflow executors for recall, note capture, reflection request, and review creation.
- Add working memory scoping by agent run/workflow run.
- Record trace ids in MAF outputs.

## Do Not Do

- Do not hardwire Cognitive Memory into private MAF context-builder internals.
- Do not bypass authorization or redaction policy.
- Do not make workflow executors directly mutate projection stores.

## Acceptance Checklist

- MAF context is supplied through a general extension point.
- Tools and executors return traceable, bounded context.
- Existing MAF behavior remains compatible when memory is disabled.

## Proof Required

- MAF adapter tests.
- Workflow executor tests.
- Trace evidence for context-pack injection.

## Browser Validation Logging

- Browser proof is optional unless workflow UI changes are included.
- If included, capture workflow route and evidence in `reviews/01-execution-report.md`.

## Progression Gate

- Proceed to UI only after MAF integration is traceable and policy-aware.

## Suggested Agent Prompt

- Integrate Cognitive Memory with MAF and workflows through extension contracts without moving durable memory policy into MAF.
