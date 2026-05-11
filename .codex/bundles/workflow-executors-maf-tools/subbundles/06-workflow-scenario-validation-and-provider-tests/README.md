# Workflow scenario validation and provider tests

## Status

- `Ready`

## Objective

- Prove the executor system with at least 20 realistic workflow examples and required model/provider checks.

## Success Criteria

- Execution report lists 20 scenarios with executor mix, inputs, expected output, actual result, and proof.
- Scenarios cover success and non-happy paths for storage, HTTP, spreadsheet, project-structure, image, timeout, retry, and validation.
- `gpt-5-mini` and Ollama `gptoss20b64k` attempts are run and recorded with exact result/blocker.

## Covered Inputs

- R05, R06, R07, R08, R09, R11, R12, R16, R17.

## Prerequisites

- Subbundles 01 through 05 are closed or explicitly marked with blockers.
- Test fixtures for files/workbooks and local HTTP endpoint are available.

## Exact Source References

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`
- `C:\repositories\CanDoItAll\.codex\bundles\workflow-executors-maf-tools\artifacts\workflow-executors-plan.xlsx`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\MafInProcessWorkflowExecutionBackend.cs`

## Deliverables

- Scenario matrix with at least 20 rows in execution report and xlsx plan.
- Automated tests for core scenarios where practical.
- Manual/provider command evidence for gpt-5-mini and Ollama.
- Browser proof from subbundle 05 linked into final validation.

## Dependency Impact

- Subbundle 07 relies on this proof to decide closure and follow-ups.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Create or reuse scenario fixtures for files, workbook, local HTTP endpoint, and workflow definitions.
2. Run automated workflow executor tests.
3. Execute or simulate through the runtime only where service/provider absence is explicitly recorded.
4. Run provider checks for `gpt-5-mini` and `ollama run gptoss20b64k` or an equivalent local model availability command.
5. Fill the scenario table in `reviews/01-execution-report.md`.
6. Record commands and artifact paths.

## Scope Exceptions

- If external credentials or local model files are unavailable, record the exact blocker and preserve the scenario as blocked rather than fake success.

## Do Not Do

- Do not count descriptor-only catalog tests as real workflow scenarios.
- Do not duplicate trivial variants to inflate the scenario count.
- Do not skip provider attempts silently.

## Acceptance Checklist

- 20 scenario rows exist with meaningful variety.
- At least one non-happy path proves timeout/retry/failure semantics.
- Provider attempts include command/output summary.
- Scenario proof links to tests, logs, artifacts, or browser screenshots.

## Proof Required

- Targeted workflow executor tests.
- `ollama list` and `ollama run gptoss20b64k` or documented local blocker.
- gpt-5-mini provider attempt through existing app/provider path or documented credential blocker.
- Scenario matrix in execution report.

## Browser Validation Logging

- Use evidence from subbundle 05 and confirm it remains valid after final changes.

## Progression Gate

- Subbundle 07 may close only if every requested raw note is either proven or explicitly blocked with a concrete reason and follow-up.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
