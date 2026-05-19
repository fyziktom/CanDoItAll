# 07-end-to-end-quality-corpus-and-closure

## Status

- `Ready`

## Objective

Prove the hardened quality foundation end to end and synchronize bundle closure with the prior completion claim.

## Success Criteria

- A representative regression corpus proves duplicates, repeat runs, contradictions, temporal supersession, multi-project isolation, restricted/redacted content, generated-only inputs, unsupported modes, and dry runs.
- Full CognitiveMemory-targeted unit and integration test filters pass.
- CognitiveMemory and migration projects build.
- Follow-up execution report has completed gate rows, proof commands, and raw-note closure.
- Prior bundle closure is updated or qualified so future agents do not trust the original "completed" claim without this follow-up.

## Covered Inputs

- H-14, H-15, H-16, and final closure for all requirements.

## Prerequisites

- Subbundles 01 through 06 complete and progression gates passed.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-quality-foundation-hardening-followup\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-quality-foundation-dreaming-synthesis\README.md`
- `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-quality-foundation-dreaming-synthesis\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\CanDoItAll.Modules.CognitiveMemory.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.Sqlite\CanDoItAll.Migrations.Sqlite.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj`

## Deliverables

- End-to-end corpus tests or fixtures covering all listed adversarial cases.
- Final proof commands and results in the execution report.
- Raw-note closure rows updated from `Planned` to `Solved`, `Partially solved`, or `Not solved`.
- Completed-stage bundle validator pass.
- Prior bundle closure note or README/execution report update.

## Dependency Impact

- This is the final closure gate. It does not unlock further subbundles; it decides whether the follow-up can honestly be marked complete.

## Validation Depth

- End-to-end regression and closure.

## Implementation Steps

1. Review gate rows for Subbundles 01 through 06.
2. Add or consolidate corpus tests covering the adversarial cases.
3. Run full CognitiveMemory-filtered unit and integration tests.
4. Run module and migration project builds.
5. Update this follow-up execution report and raw-note closure.
6. Update or qualify the prior bundle completion claim.
7. Run prepared/completed bundle validators as appropriate.

## Scope Exceptions

- If a UI is not added, browser validation remains N/A with a clear note.
- If an adversarial case is impossible to model with current public APIs, record the exact blocker and owning follow-up.

## Do Not Do

- Do not mark closure passed with pending gate rows.
- Do not treat structural bundle validation as a substitute for tests.
- Do not leave the prior bundle claiming unqualified completion if this follow-up found material gaps.

## Acceptance Checklist

- Every requirement H-01 through H-16 has closure status and proof.
- All subbundle gate rows are no longer pending.
- Full CognitiveMemory unit and integration filters pass.
- Module and migration builds pass.
- Completed-stage bundle validator passes or blocker is recorded honestly.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal" -m:1`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal" -m:1`
- `dotnet build src\CanDoItAll.Modules.CognitiveMemory\CanDoItAll.Modules.CognitiveMemory.csproj --no-restore -m:1`
- `dotnet build src\CanDoItAll.Migrations.Sqlite\CanDoItAll.Migrations.Sqlite.csproj --no-restore -m:1`
- `dotnet build src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj --no-restore -m:1`
- `python codex\skills\bundles\candoitall-bundle-preparation\scripts\validate_bundle.py --stage completed --profile initiative codex\bundles\cognitive-memory-quality-foundation-hardening-followup`

## Browser Validation Logging

- N/A unless implementation adds UI. If UI is added, record route, viewport, Playwright actions, screenshots, and screenshot review answers before closure.

## Progression Gate

- The bundle can close only when all proof commands pass, raw notes are closed note by note, and the prior bundle completion claim is qualified or synchronized.

## Suggested Agent Prompt

```text
Implement subbundle 07 only after all earlier gates pass. Build the end-to-end corpus, run the full proof suite, update closure artifacts, run the completed-stage validator, and do not close with pending proof.
```
