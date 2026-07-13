# Template And Process Hardening

## Status

- `Ready`

## Objective

- Refactor process templates and step instructions so they use typed readiness/driver policy, avoid brittle prose-only orchestration, and stop overfitting to Calculator, Tetris, Blazor, or browser-proof-only development scenarios.

## Success Criteria

- Software-delivery templates support .NET delivery for arbitrary app topics.
- Browser/screenshot proof is required only for UI-visible steps that declare it.
- Management-only steps can suppress development skills/tools.
- Long repeated prompt fragments are replaced or backed by testable policy descriptors where feasible.

## Covered Inputs

- R08 Template Hardening.
- R07 .NET Delivery Driver Isolation.
- R03 Capability Readiness Contract.
- User concern that the multi-team development process is .NET-focused but must not be hardcoded to a specific app topic.

## Prerequisites

- SB01 completed.
- SB02 completed.
- SB03 completed.
- SB04 completed.

## Exact Source References

- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/dotnet-solution-setup/definition.json`
- `repo://Templates/Processes/processes/dotnet-solution-setup/steps/create-dotnet-project.md`
- `repo://Templates/Processes/processes/dotnet-solution-setup/steps/add-test-project.md`
- `repo://Templates/Processes/processes/dotnet-solution-setup/steps/validate-first-build.md`
- `repo://Templates/Processes/processes/dotnet-development-slice/definition.json`
- `repo://Templates/Processes/processes/dotnet-ui-screenshot-writeback/definition.json`
- `repo://Templates/Agents/teams/dotnet-delivery`
- `repo://Templates/Agents/teams/visual-automation-templates`
- `repo://tests/Integration/CanDoItAll.Tests.Integration`

## Deliverables

- Template audit identifying instruction-only gates that should become typed readiness or driver policy.
- Updated templates that declare capability needs and suppressions explicitly.
- Reduced duplicate .NET scaffold/build/test instruction text where driver policy can supply deterministic details.
- Fixture set covering UI app, non-UI app/service/library, and management-only process steps.
- Tests that parse templates and validate required readiness declarations.

## Dependency Impact

- SB06 depends on hardened templates to run realistic E2E scenarios.
- Weak template proof can cause E2E failure even if runtime foundations are correct.
- Template changes must be reversible and narrow because they affect live process runs.

## Validation Depth

- Process-template critical closure.

## Implementation Steps

1. Inventory all software-delivery and .NET process steps that require tools, MCPs, skills, screenshots, or managed artifacts.
2. Convert requirement-only prompt text into typed readiness declarations where the contract supports it.
3. Remove or reduce repeated deterministic .NET policy text that SB04 made driver-owned.
4. Add explicit suppression declarations for management-only steps that should not receive development context.
5. Ensure UI/browser proof steps declare browser/Playwright capability without forcing it on non-UI steps.
6. Add parser tests for template readiness declarations and fixture diversity.
7. Update template docs or execution notes with migration compatibility where old launch variables remain.

## Scope Exceptions

- Do not change generic runtime behavior here.
- Do not attempt a full process-definition redesign beyond the paths required for escalation root causes.

## Do Not Do

- Do not hardcode Calculator or Tetris in templates except as named test fixture data.
- Do not add screenshot or Playwright proof to every development step.
- Do not remove useful process guidance when no typed policy replacement exists.
- Do not make prompt text the only source of a required capability.

## Acceptance Checklist

- Template parser tests pass for software-delivery, .NET solution setup, .NET development slice, and screenshot writeback processes.
- UI-visible steps declare browser/screenshot readiness.
- Non-UI steps do not require browser/screenshot readiness.
- Management-only step suppression is represented in template/readiness data.
- Launch variables remain backward-compatible or migration notes document removals.

## Proof Required

- `dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter Template`
- `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter Capability`
- Template parse/audit transcript listing required capabilities by step.
- Diff review showing no Calculator/Tetris hardcoding added to production templates.

## Browser Validation Logging

- N/A for UI rendering. Record only template audit evidence and, if a browser-proof process template changes, the declared browser capability paths that SB06 must exercise.

## Progression Gate

- SB06 may start only when template readiness declarations are parseable and fixture coverage includes UI, non-UI, and management-only paths.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Use the typed readiness and domain driver policy from earlier subbundles to harden process templates. Keep template edits narrow, parseable, and fixture-agnostic. Prove UI/browser capability is required only where declared, non-UI steps are not forced into screenshot proof, and management-only steps suppress development context. Stop if a required capability remains prompt-only.
```
