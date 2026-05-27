# SB15: Generic Process And Agent Training Template Regression

## Status

- Completed

## Objective

Protect generic non-software and agent-training process behavior after MAF/process fixes.

## Covered Inputs

- RQ09: protect generic non-software and agent-training process flows.

## Prerequisites

- SB13 recovery/approval correctness must be complete.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplateLibraryService.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/WorkflowSubprocessArtifactMapper.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs

## Deliverables

- Regression proof for non-software templates and agent-improvement/training style processes.
- Proof that artifact validation remains generic and not Blazor/software-specific.

## Dependency Impact

- SB16 stabilization and SB18 release readiness depend on generic process coverage.

## Validation Depth

- Critical semantic proof must include at least one non-software or business/training style process path.

## Implementation Steps

- Run template and lint tests for generic process flows.
- Add tests if validation assumes software artifacts.
- Verify workflow/subprocess bridge works for business artifacts.
- Update `proof/SB15`.

## Do Not Do

- Do not make artifact validation depend on software artifact names or file extensions.
- Do not narrow generic process behavior to the Blazor/Tetris case.

## Acceptance Checklist

- Generic process tests pass.
- Artifact validation remains domain-neutral.
- Proof artifacts are updated.

## Proof Required

- Failing-first/adversarial transcript for software-specific assumptions.
- Passing generic process transcript.
- Source assertions, anti-stub audit, and hashes.

## Browser Validation Logging

- N/A unless generic process UI changes are made.

## Progression Gate

- SB16 may start only after generic process regressions pass.

## Suggested Agent Prompt

Run and strengthen generic process and agent-training regressions so process artifact validation remains domain-neutral.

## Closure Proof

- bundle://proof/SB15/manifest.md
- bundle://proof/SB15/semantic-invariants.md

