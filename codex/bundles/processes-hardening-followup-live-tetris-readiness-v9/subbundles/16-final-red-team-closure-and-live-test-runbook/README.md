# SB16: Final Red Team Closure And Live Test Runbook

## Status

- Status: `Completed`

## Objective

Run final red-team closure and deliver a live-test runbook for creating any user-specified Blazor WASM PWA application through the generic Processes path.

## Covered Inputs

- RQ09 red-team checks.
- RQ10 live-test runbook.

## Prerequisites

- SB15 final genericity checkpoint is complete.

## Exact Source References

- `repo://Templates/Processes`
- `repo://codex/skills/candoitall-api-processes/SKILL.md`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `bundle://reviews/01-execution-report.md`

## Deliverables

- Final source assertion and red-team report for topic-neutral reusable instructions.
- Live-test runbook that explains how to supply any app topic through project structure or run prompt.
- Final raw-note closure and completed-stage validation result.

## Dependency Impact

- This is the final closure gate for the bundle.

## Validation Depth

- Critical closure. Require Semantic Adequacy Gate proof, final validator, targeted tests, source assertions, and anti-stub audit.

## Implementation Steps

1. Run final genericity red-team search.
2. Run targeted test suite and required source audits.
3. Write the runbook using app-topic placeholders.
4. Update execution report and raw-note closure.
5. Run completed-stage bundle validation.

## Do Not Do

- Do not claim readiness if any reusable template or skill still contains a fixed app topic.
- Do not mark partial proof as solved.

## Acceptance Checklist

- Final validator passes or explicit blocker is recorded.
- Runbook uses generic placeholders for app topic and acceptance criteria.
- Raw notes are closed as Solved, Partially solved, or Not solved with proof.
- Final report aligns code changes, tests, source assertions, and bundle status.

## Proof Required

- `proof/SB16/manifest.md`
- `proof/SB16/semantic-invariants.md`
- `proof/SB16/transcripts/failing-first.txt`
- `proof/SB16/transcripts/passing.txt`
- `proof/SB16/transcripts/anti-stub-audit.txt`

## Browser Validation Logging

- Record final browser validation analytics if UI was changed or explicitly record API-only/no-browser blocker in `bundle://reviews/01-execution-report.md`.

## Progression Gate

- Bundle may close only after final red-team, raw-note closure, proof manifests, execution report, and completed-stage validator agree.

## Suggested Agent Prompt

Run final red-team closure for generic Blazor WASM PWA readiness and produce a live-test runbook that accepts any user-supplied app topic.
