# Validation And Operator Proof

## Status

- `Completed`

## Objective

Run focused and broader validation that proves package upgrade, default model migration, A2A/handoff cooperation, tool availability, context policy, and process artifact handoff.

## Covered Inputs

- `REQ-01`
- `REQ-02`
- `REQ-03`
- `REQ-04`
- `REQ-05`
- `REQ-06`
- `REQ-07`
- `REQ-08`
- `REQ-09`
- `REQ-10`
- `REQ-12`

## Prerequisites

- Architecture review gate 2 returned `Proceed`.
- All implementation subbundles are complete or have accepted documented exceptions.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll.slnx`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj`
- `C:\repositories\CanDoItAll\codex\bundles\maf-1-3-a2a-handoffs\reviews\01-execution-report.md`

## Deliverables

- Updated execution report with command outcomes.
- Targeted test proof for every changed contract.
- Browser proof only for changed visible UI.
- Residual risk list with concrete owner or reopen decision.

## Dependency Impact

- Final architecture review depends on this proof.

## Validation Depth

- End-to-end regression and closure.

## Implementation Steps

1. Run targeted build/test commands from each subbundle.
2. Run broader build/test if targeted proof is clean.
3. If UI changed, run browser validation and capture screenshots.
4. Update execution report with exact outcomes.
5. Reopen failed subbundles instead of marking validation green.

## Scope Exceptions

- If environment credentials are missing for live OpenAI/A2A, use deterministic mocks and record live-provider validation as residual risk.

## Do Not Do

- Do not ignore failing tests by narrowing filters after a relevant failure.
- Do not substitute static inspection for runtime proof where tests are feasible.

## Acceptance Checklist

- Package upgrade proof recorded.
- Default model proof recorded.
- A2A/handoff proof recorded.
- Tool profile proof recorded.
- Process artifact handoff proof recorded.
- Context/session proof recorded.

## Proof Required

- `dotnet restore CanDoItAll.slnx`
- `dotnet build CanDoItAll.slnx --no-restore -m:1`
- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore -m:1`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore -m:1`
- Browser validation commands/screenshots only if UI changed.

## Completion Notes

- Initial restore surfaced a real `NU1605` downgrade after the A2A hosting package introduced `Microsoft.Extensions.*` `10.0.1`; `CanDoItAll.Mcp.Processes` now references `Microsoft.Extensions.Hosting` and `Microsoft.Extensions.Logging.Console` `10.0.1`.
- Initial unit run caught the root execution-report fixture, generic proof wording, local Playwright artifact scanning, and a raw test secret fixture; all were fixed and the full unit project passed.
- Initial integration run caught the `DispatchCandidate` test reflection helper and an invalid software-delivery baseline branch transition; both were fixed with explicit process cooperation metadata and explicit QA branch selection.
- Final proof: restore passed, solution build passed, full unit tests passed, full integration tests passed, and `git diff --check` passed with LF-to-CRLF warnings only.
- Browser validation was not required because this bundle changed runtime, templates, metadata, process dispatch, tests, and docs, not visible Blazor UI.

## Browser Validation Logging

- Required only for visible UI changes. Record route, viewport, actions, assertions, screenshot path, and result in the execution report.

## Progression Gate

- Final closure may start only when failures are fixed, accepted as environment blockers, or converted into explicit remediation subbundles.

## Suggested Agent Prompt

```text
Execute subbundle 11 only: run targeted and broader validation for the completed implementation, update the execution report, and reopen failed subbundles instead of hiding failures.
```
