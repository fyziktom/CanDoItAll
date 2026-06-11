# SB05: Live OpenAI template smoke

## Objective
Add a bounded live provider proof for a representative template path, or skip honestly.

## Exact source references
- repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch

## Implementation steps
1. Add an opt-in live template smoke using a very small representative process path.
2. Require explicit env variables:
   - `CANDOITALL_RUN_LIVE_PROCESS_TEMPLATE_VALIDATION=true`
   - `CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE=true`
   - explicit model
   - explicit timeout
   - explicit max tokens
3. Use `ProcessesService` / launch plan / dispatch path, not workspace-only chat.
4. Verify process run id, step id, execution run, provider/model, usage, finalizer, and artifact/readback.
5. If env is absent, skip and classify as skipped; do not count as live proof.

## Acceptance checklist
- No API key value logged.
- Token/time budget bounded.
- Skipped live test cannot be reported as live pass.
- Deterministic process-mock tests remain primary CI proof.
