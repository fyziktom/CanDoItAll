# 04 Mock Agent Failure Matrix

## Status

- `Completed`

## Objective

Improve deterministic mock agents so they cover the real failure classes before another expensive process proof is attempted.

## Covered Notes

- User asked to improve mock agents to cover possible failures.
- Observed failures include repeated writes, missing validation, and missing artifacts.

## Prerequisites

- Subbundle 02 required artifact contract is green.
- Subbundle 03 retry ownership classification is green or blocked with an explicit exception.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ProcessMockAgentRuntime.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ScenarioHarnessAgentRuntime.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ScenarioHarnessSupport.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessMockAgentRuntimeIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`

## Scope

- Add deterministic modes or fixtures for observed failures.
- Cover recovery success after explicit directive.
- Keep mocks strongly typed and controlled by clear scenario keys/options.

## Dependency Impact

- Provides the deterministic substrate for phase 05.

## Validation Depth

- Integration tests for every failure mode.
- Existing happy-path mock process tests remain green.

## Implementation Steps

1. Inventory current mock roles and scenario harness modes.
2. Add scenario keys or options for missing current artifact, missing upstream artifact, repeated-write failure, validation omission, and recovery success.
3. Ensure each mode emits enough structured evidence for process read models.
4. Add tests for each mode and the normal happy path.

## Scope Exceptions

- Do not simulate real LLM token-level behavior; simulate process-visible outcomes.

## Do Not Do

- Do not make mocks depend on external providers.
- Do not add random/flaky behavior.
- Do not make happy-path tests weaker.

## Acceptance Checklist

- Mock matrix includes observed failure modes.
- Recovery success can be proven deterministically.
- Happy path remains green.
- Tests are isolated and fast enough for normal iteration.

## Proof Required

- Focused integration tests.
- Existing mock runtime integration tests.
- Execution report updated.

## Browser Validation Logging

- N/A unless UI behavior changes.

## Progression Gate

- Proceed to subbundle 05 only after deterministic mock coverage can reproduce and recover the target failure classes.
- Stop and repair this subbundle if the observed repeated-write, missing-validation, or missing-artifact cases cannot be simulated deterministically.

## Suggested Agent Prompt

```text
Implement subbundle 04 only. Extend deterministic process mock agents to cover repeated-write, missing-validation, missing-current-artifact, missing-upstream-artifact, and recovery-success modes.
```
