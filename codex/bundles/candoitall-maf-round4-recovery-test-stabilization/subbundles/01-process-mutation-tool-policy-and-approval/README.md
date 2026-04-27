# 01 — Process Mutation Tool Policy and Approval


## Problem

Process mutation tools currently default to `Read` classification because `IsMutationTool(...)` only recognizes workspace mutation tools. Process tools can create/update/delete definitions, start runs, transition steps, resolve assignments, and record artifacts.

## Tasks

1. Introduce a central metadata catalog for all tool names. Avoid scattering string lists across runtime and tests.
2. Classify process tools:
   - Read: list/get/export/analytics/template read tools.
   - Mutation: save/publish/delete/import/start/transition/resolve/record/template import.
   - Validation: build/test/run proof tools.
   - Finalizer: `submit_*` finalizer tools.
3. Ensure function-calling middleware uses this catalog.
4. Ensure finalizer sequence validation treats process mutations as significant side effects.
5. Decide and implement approval behavior for process mutations:
   - In user-facing agent runs, mutation tools must require approval unless explicitly allowed.
   - In governed automation, any auto-approval must be explicit in `ExecutionInvocationPolicy` and tested.
6. Do not break read-only process tools.

## Acceptance criteria

- Every process tool created in `MafAgentRuntime.ProcessTools.cs` has exactly one classification.
- No unknown `processes_*` tool silently defaults to `Read`.
- Mutation process tools are blocked/approved according to policy.
- Finalizer sequence validator sees process mutations after finalizer as invalid where applicable.

## Suggested tests

- `AgentToolInvocationPolicyTests.Process_mutation_tools_are_classified_as_mutation`
- `AgentToolInvocationPolicyTests.Process_read_tools_are_classified_as_read`
- `MafAgentRuntimeTests.Process_mutation_tools_require_approval_when_policy_requires_it`
- `AgentFinalizerSequenceValidatorTests.Process_mutation_after_required_finalizer_is_rejected`

