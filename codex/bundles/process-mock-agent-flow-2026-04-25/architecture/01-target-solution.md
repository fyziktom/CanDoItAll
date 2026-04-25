# Target Solution

## Target Shape

- Add `ProcessMockAgentOptions` under `AgentFramework:ProcessMockAgents` with `Enabled` defaulting to false.
- Add a deterministic mock provider catalog with role-specific mock agents only when the option is enabled.
- Add `ProcessMockAgentRuntime` as an `IAgentRuntime` decorator around the existing scenario/MAF runtime chain.
- Match the mock provider by a stable base URL such as `process-mock://agents`.
- Return governed `PROCESS_STEP_OUTCOME` markers so `ProcessRunAutomationDispatchService` continues to handle transitions.

## Role Model

- Product owner mock agent writes scope and acceptance criteria for the calculator app.
- Architect mock agent writes architecture notes and handoff constraints.
- Developer mock agent writes an intentionally incomplete calculator implementation artifact.
- QA mock agent rejects the first implementation with branch outcome `repairs-required`.
- Repair developer mock agent writes the corrected calculator implementation artifact.
- QA mock agent approves the repaired output with branch outcome `approved`.
- Release mock agent writes release notes after approval.

## Artifact Strategy

- Use paths under `artifacts/process-mock/<process-run-or-execution-run>/...`.
- Write all artifacts through `IWorkspaceFileService`.
- Use deterministic content so tests can assert exact snippets and branch outcomes.

## Boundaries

- Do not add mock behavior to `ProcessRunAutomationDispatchService`.
- Do not modify real provider execution.
- Do not enable mock agents by default.
- Do not make the process engine depend on calculator-specific concepts.

## Validation Strategy

- Validate options and catalog seeding.
- Validate direct mock runtime behavior and artifact writing.
- Validate a process flow that performs a QA rejection and a repair before approval.
- Run targeted tests for AgentFramework and Processes integration, plus a build if targeted tests pass.
