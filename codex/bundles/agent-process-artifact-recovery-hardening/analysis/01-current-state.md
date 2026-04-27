# Current State

## Real Failure Shape

The real run failed in the implementation step after five attempts. The process recorded implementation change-set artifacts and build/test transcript artifacts, but did not record the required `Migration and rollout preparation checklist`.

The console logs show the implementation agent hit the repeated-tool guard multiple times:

- Repeated identical write to `Calculator/Components/Pages/Home.razor`.
- Repeated identical write to `Calculator.Tests/CalculatorEngineTests.cs`.
- Missing required validation tools on some attempts: `workspace_dotnet_build`, `workspace_dotnet_test`.
- `workspace_dotnet_new` was denied in at least one attempt.

This is not just an artifact-projection bug. The implementation lane was unstable and did not complete the required critical path.

## Template Contract

`Templates\Processes\processes\software-delivery\steps\implementation.md` states the implementation step must produce:

- `Implementation change set`
- `Migration and rollout preparation checklist`

The checklist validation says it must name data changes, operational preconditions, and rollback steps. For a DB-free calculator app, a valid checklist should say:

- No schema or data migration is required.
- No data backfill or rollback data step exists.
- Rollout preconditions are build/test pass and app smoke validation.
- Rollback is reverting the generated app/change set or restoring the previous project state.

## Dispatch Prompt State

`ProcessRunAutomationDispatchService.BuildExecutionPromptCore` already includes:

- Required output artifact summary.
- Required response sections for every required artifact.
- Implementation critical path.
- Rule not to write implementation artifacts before source changes and validation pass.

The gap is that the prompt still lets the agent get trapped in validation/rewriting loops and does not provide a crisp artifact completion checklist after validation succeeds or when a DB-free migration checklist is required.

## Mock Coverage State

`ProcessMockAgentRuntime` is deterministic and currently supports happy-path process roles. It does not yet cover the observed real-agent failure matrix:

- Agent repeats unchanged writes.
- Agent omits required build/test validation.
- Agent writes implementation files but omits a required checklist.
- Current step is missing its own artifact.
- Current step is blocked because an upstream required input artifact is missing.

## Retry Routing State

The dispatcher retries an incomplete/failed current step. That is correct when the current step owns the missing artifact. It is wrong when the missing artifact is an upstream input that the current step cannot produce. The user called out this distinction explicitly.

The implementation must classify artifact gaps by ownership before deciding whether to retry current step, reopen upstream step, or block with an operator action.
