# 01-current-state-regression-and-gap-baseline

## Status

- Status: `Completed`

## Closure Notes

- Prepared-stage bundle validator passed after metadata repair.
- Restore passed; first build failure was a host file lock from a running `CanDoItAll.Web` process, then the rerun build passed after stopping that process.
- Targeted unit, component, and integration baselines passed.
- Missing Office365 address executor and Scheduler typed-input contract are captured as failing-first evidence.
- Proof manifest: `bundle://proof/SB01/manifest.md`
- Semantic invariants: `bundle://proof/SB01/semantic-invariants.md`

## Objective

Confirm the pushed workflow executor catalog state and create a reliable baseline before Office365/Scheduler changes.

## Covered Inputs

- R1-R12: baseline proof and failing-first evidence for missing Office365-by-address and Scheduler typed-input behavior.
- Original request: review the pushed implementation and prepare/execute the Office365 Scheduler follow-up.

## Prerequisites

- Repository is on the intended `processes-hardening` branch or the deviation is recorded.
- Prepared-stage bundle validation passes after structural repair.
- No production feature code has been changed before baseline capture.

## Exact Source References

- `repo://CanDoItAll.slnx`
- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs`
- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365GraphClient.cs`
- `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs`
- `repo://src/CanDoItAll.Modules.SchedulerPlanner/Pages/SchedulerPlannerPage.razor`
- `repo://Templates/Workflows/manifest.yaml`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`
- `repo://tests/CanDoItAll.Tests.Components/SchedulerPlannerPageTests.cs`

## Scope

- Capture current commit, working tree status, restore/build baseline, and targeted existing workflow tests.
- Inventory current Office365 executors, Scheduler target launch path, Scheduler UI input fields, and workflow templates relevant to project tasks/assets.
- Add failing-first tests or documented red proof for the missing Office365 address executor and Scheduler typed parameter form.

## Dependency Impact

- SB02-SB08 depend on this baseline to distinguish new regressions from pre-existing failures.
- If baseline restore/build is broken for unrelated reasons, later subbundles must record that blocker and use targeted proof only where defensible.

## Validation Depth

- Restore and build transcript.
- Targeted workflow/plugin/template/Scheduler test transcript.
- Source assertions for current Office365 executor inventory and Scheduler raw JSON behavior.
- Critical semantic proof for the baseline and missing-capability red tests.

## Implementation Steps

1. Capture `git rev-parse HEAD` and `git status --short`.
2. Run restore/build or record exact pre-existing failure.
3. Run targeted existing workflow executor/template/Scheduler tests.
4. Record Office365/Scheduler/template inventory under `bundle://proof/SB01/`.
5. Add failing-first tests or verifier transcripts for missing address executor and typed Scheduler input.

## Do Not Do

- Do not implement the new Office365 executor in SB01.
- Do not change Scheduler UX in SB01.
- Do not hide baseline failures as residual risk; record them as blockers or scoped proof limitations.

## Acceptance Checklist

- Baseline proof is present under `bundle://proof/SB01/`.
- Existing executor catalog behavior is not regressed by later work.
- Missing Office365/Scheduler capabilities are represented as failing-first or verifier evidence.

## Proof Required

- `bundle://proof/SB01/manifest.md`
- `bundle://proof/SB01/semantic-invariants.md`
- Restore/build transcript.
- Targeted test transcript.
- Source assertion transcript.
- Anti-stub audit transcript.

## Browser Validation Logging

- N/A unless baseline reveals an existing Scheduler or Workflows UI regression that must be captured before implementation.

## Progression Gate

- Continue to SB02 only after baseline proof exists and any unrelated failures are recorded with exact commands, exit codes, and downstream impact.

## Suggested Agent Prompt

Execute SB01 by proving the current workflow executor catalog and Scheduler baseline, then add red evidence for the missing Office365 address executor and typed Scheduler input form before any feature implementation.
