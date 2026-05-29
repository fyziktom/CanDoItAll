# 08-agent-subworkflow-and-artifact-node-policy

## Objective

Resolve pass-through ambiguity for helper node kinds.

## Required work

1. Audit all `WorkflowNodeKind` values:
   - Start
   - LlmCall
   - Triage
   - StrictLogic
   - Executor
   - Artifact
   - HumanInput
   - AgentStep
   - Subworkflow
   - End
2. Define for each:
   - executable semantics,
   - visual-only semantics,
   - or validation-blocked status.
3. Implement or block:
   - `Artifact` as artifact/write-reference node or require executor-backed configuration.
   - `AgentStep` as future/blocked unless a safe agent invoker exists.
   - `Subworkflow` as future/blocked unless runtime bridge and recursion limits exist.
   - `StrictLogic`/`Triage` as deterministic routing/transform helpers or document pass-through semantics.
4. Add validation tests that active unsupported helper nodes cannot publish/run silently.

## Acceptance checklist

- No active node kind silently returns input unless that is the explicitly documented behavior.
- UI labels and validation messages explain what to do.
