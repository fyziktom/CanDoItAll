# Execution Observation Repair And Final Proof

## Status

- `Completed`

## Objective

- Execute or simulate at least 20 real-world workflow scenarios, compare expected decisions with `gpt-5-mini` and Ollama `gptoss20b64k`, record observed issues, repair failures, and close the follow-up bundle with evidence.

## Covered Inputs

- RQ-028 plus closure proof for RQ-021 through RQ-027.

## Prerequisites

- Subbundles 06-08 implemented.
- Ollama with `gptoss20b64k` available locally, or blocker recorded with command output.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\workflow-basic-routing-maf\reviews\evidence`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj`

## Deliverables

- Scenario observation log with at least 20 cases.
- Model comparison output for `gpt-5-mini` and Ollama `gptoss20b64k`.
- Browser screenshots for decision nodes, setup dialogs, and example workflows.
- Updated execution report with trouble log and repair notes.

## Validation Depth

- Full closure proof for follow-up implementation.

## Dependency Impact

- This subbundle closes RQ-021 through RQ-028 and feeds observed issues back into implementation or bundle notes.
- The first failed model comparison directly changed seeded LLM prompt guidance and the recorded production prompt contract.
- No remaining downstream subbundle is blocked after this closure.

## Implementation Steps

1. Run targeted unit/component/integration tests.
2. Run or simulate 20+ real-world workflow routing scenarios.
3. Query `gpt-5-mini` and local Ollama `gptoss20b64k` for comparable decisions where available.
4. Record mismatches, runtime errors, UI issues, and datasource issues.
5. Repair implementation or bundle notes.
6. Capture browser screenshots and final validator proof.

## Do Not Do

- Do not claim live external service execution if only structural/simulated proof ran.
- Do not ignore observed workflow or UI trouble.

## Acceptance Checklist

- 20+ scenarios have expected and actual decisions recorded.
- Model comparison is present or blocked with exact command/error.
- Browser screenshots prove decision-node visuals and setup dialogs.
- Final execution report names all remaining risks.

## Proof Required

- Targeted test commands.
- Browser evidence under `reviews/evidence/subbundle-09/`.
- Scenario/model comparison artifacts.

## Closure Proof

- Web build passed with 0 warnings and 0 errors.
- Targeted unit tests passed: 25 workflow executor/catalog tests.
- Targeted component tests passed: 7 workflow page tests.
- Targeted integration tests passed: 11 workflow API/process executor tests.
- First model-comparison run found negative-name prompt ambiguity at 18/20 for both models. The prompt contract and seeded LLM instructions were repaired.
- Final model comparison passed: `gpt-5-mini-2025-08-07` 20/20, `gptoss20b64k:latest` 20/20, agreement 20/20.
- Evidence: `reviews/evidence/subbundle-09/model-comparison-20-scenarios.md`, `model-comparison-20-scenarios.json`, and `model-comparison-20-scenarios-first-run.md`.

## Browser Validation Logging

- Route: `/agents/workflows`.
- Evidence files under `reviews/evidence/subbundle-09/`.

## Progression Gate

- Bundle can return to completed only after this subbundle has concrete proof or explicit blockers.

## Suggested Agent Prompt

```text
Implement subbundle 09 only: run tests, observe workflows, compare models, repair observed trouble, and close the execution report with evidence.
```
