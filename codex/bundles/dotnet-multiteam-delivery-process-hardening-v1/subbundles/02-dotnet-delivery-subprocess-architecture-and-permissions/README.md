# .NET delivery subprocess architecture and permissions

## Status

- `Ready`

## Objective

Make the parent multi-team delivery template .NET-aware, add architecture design/review as a subprocess, and harden operation contracts so non-implementation steps cannot mutate product code.

## Covered Inputs

- R01, R02, R03, R04, R05, R06

## Prerequisites

- SB01 prepared-stage gate passed.

## Exact Source References

- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/dotnet-development-slice/definition.json`
- `repo://Templates/Processes/manifest.json`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessCatalogWarmupService.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessSubprocessIntegrationTests.cs`

## Deliverables

- New `dotnet-architecture-design-review` process template.
- `software-delivery` app-type contract step and .NET architecture subprocess step.
- `software-delivery` implementation step converted to a .NET subprocess.
- `dotnet-development-slice` QA aggregation step no longer mutates product target.
- Tests proving subprocess references and non-mutating roles.

## Dependency Impact

- SB03 relies on the parent .NET flow and role permissions. Weak proof here would let screenshot/writeback subprocesses run with the wrong authority or before the correct app type is known.

## Validation Depth

- Critical foundation

## Implementation Steps

1. Create `dotnet-architecture-design-review` with separate design and review steps.
2. Update manifest and default warmup order before `software-delivery`.
3. Update `software-delivery` to resolve .NET app type and call the architecture and implementation subprocesses.
4. Remove mutation from QA/review-class steps that should only validate or record evidence.
5. Add tests that assert app-type contract text, subprocess steps, and operation contracts.

## Scope Exceptions

- No JavaScript process changes.

## Do Not Do

- Do not add runtime branching code.
- Do not let architecture, QA, or manager writeback steps mutate product files.
- Do not start a live process run.

## Acceptance Checklist

- New architecture template loads through the pack loader.
- `software-delivery` implementation is subprocess-backed.
- Architecture design/review steps use read-only or managed-artifact-only target scopes.
- Tests fail if architect/QA/writeback steps gain `MutateProductTarget`.

## Proof Required

- Changed-file hashes in SB02 proof manifest.
- Source assertions for operation contracts and subprocess keys.
- Targeted test transcript.
- Anti-stub audit showing no placeholder process keys or TODOs.

## Browser Validation Logging

- N/A. This subbundle changes process template contracts only.

## Progression Gate

- Downstream work may continue only after template import/projection tests can resolve the new subprocess references and permission assertions pass.

## Suggested Agent Prompt

```text
Implement SB02 only. Add the .NET architecture subprocess and parent process permission hardening. Keep all non-implementation roles non-mutating and prove it with tests.
```
