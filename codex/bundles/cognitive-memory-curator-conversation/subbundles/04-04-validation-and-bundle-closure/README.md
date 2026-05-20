# 04 Validation And Bundle Closure

## Status

- State: `Completed`
- Critical foundation: `No`

## Objective

Run final validation, audit raw-note closure, record proof, and synchronize bundle status.

## Covered Inputs

- All requirements `R-001` through `R-009`.
- All raw notes `N001` through `N008`.

## Prerequisites

- Subbundles 01, 02, and 03 closure gates passed.
- Browser validation analytics from subbundle 03 recorded.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-curator-conversation\README.md`
- `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-curator-conversation\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-curator-conversation\inputs\00-original-request.md`
- `C:\repositories\CanDoItAll\CanDoItAll.slnx`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`

## Deliverables

- Final test/build command results.
- Final browser analytics and screenshot references.
- Raw-note closure table with `Solved`, `Partially solved`, or `Not solved`.
- Bundle README validation summary updated.
- Final prepared/completed validators run.

## Dependency Impact

- This is the final closure phase.
- Any failed proof reopens the owning subbundle.

## Validation Depth

- Full targeted tests.
- Build, browser analytics review, raw-note audit, and validators.

## Implementation Steps

1. Run targeted unit and component tests.
2. Run build or solution-level validation as practical.
3. Re-run browser checks if code changed after UI proof.
4. Audit every raw note against code and proof.
5. Update execution report and root README.
6. Run `scripts/validate_bundle.py --stage completed`.

## Scope Exceptions

- If external voice provider proof is unavailable, mark only real audio-provider proof as partial and cite test/UI proof for the shipped path.

## Do Not Do

- Do not close a raw note as solved without proof.
- Do not hide failed browser proof in residual risk.
- Do not leave executed subbundles as `Ready` or `In progress`.

## Acceptance Checklist

- Tests/build results recorded.
- Browser analytics rows are complete.
- Raw notes are closed one by one.
- Final validators pass or blockers are explicit.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- `dotnet build CanDoItAll.slnx`
- `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py codex\bundles\cognitive-memory-curator-conversation --profile initiative --stage completed`

## Browser Validation Logging

- Review and complete the row created by subbundle 03.

## Progression Gate

- The bundle is complete only when code, tests, browser evidence, gate decisions, and raw-note closure agree.

## Suggested Agent Prompt

Execute final closure only. Run the required proof, audit each raw note, update bundle status, and run the completed-stage validator.
