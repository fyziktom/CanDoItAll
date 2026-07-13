# 01-tracked-artifact-and-test-naming-hygiene

## Status

- `Completed`

## Objective

Resolve repository hygiene failures without weakening the repository guardrails.

## Covered Inputs

- RH-001: tracked transient bundle artifacts under `codex/bundles/...`.
- RH-002: active test identifiers/literals containing work-package IDs such as `SB11`, `SB30`, `SB09`, and `sb33`.

## Prerequisites

- Evidence exists: `bundle://evidence/targeted-failing-tests.txt`.
- No implementation from later subbundles is required.

## Exact Source References

- `repo://tests/Unit/CanDoItAll.Tests.Unit/RepositoryTransientArtifactHygieneTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/RepositoryNamingHygieneTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentCapabilitySetupApiIntegrationTests.cs`
- `repo://tests/Memory/CanDoItAll.Memory.Tests/HostCompositionDependencyRemovalTests.cs`
- `repo://tests/Memory/CanDoItAll.Memory.Tests/MemoryAsyncWorkerTests.cs`
- `repo://tests/Memory/CanDoItAll.Memory.Tests/MemoryEndToEndObservabilityProofTests.cs`
- `repo://codex/bundles`

## Deliverables

- Tracked transient bundle artifacts are no longer violating the hygiene guard, or a narrow durable-doc exception is justified and tested.
- Active test names/literals use behavior-language identifiers instead of work-package IDs, unless a narrow scanner exception is justified.

## Dependency Impact

- SB05 full-suite proof depends on this. If this subbundle weakens hygiene, future full-suite green status becomes untrustworthy.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Run the two hygiene tests alone and save failing-first output if current evidence is stale.
2. Inspect whether the tracked `codex/bundles/skill-tool-mcp-isolation-template-migration` files are intended durable source or accidental transient artifacts.
3. If accidental, remove them from tracked source through normal git-aware cleanup in the implementation branch. If intentional, move them to an allowed durable documentation location or add a narrow explicit rule naming that durable location.
4. Rename work-package-coded test methods and string literals to behavior names.
5. If any literal must remain because it is domain data, add a minimal scanner exception with a test proving only that context is allowed.

## Scope Exceptions

- Do not repair unrelated hygiene findings outside the failure list unless they block the same tests after this subbundle's changes.

## Do Not Do

- Do not exclude all `codex/`, all `tests/Memory`, or all string literals from scanning.
- Do not delete tracked files blindly without deciding whether they are accidental transient outputs or intentional durable artifacts.

## Acceptance Checklist

- [x] `RepositoryTransientArtifactHygieneTests` passes.
- [x] `RepositoryNamingHygieneTests` passes.
- [x] Source diff shows no broad scanner disablement.
- [x] Execution report records the decision for the tracked bundle files.

## Proof Required

- Failing-first transcript: `proof/SB01/failing-hygiene-tests.txt`.
- Passing transcript: `proof/SB01/passing-hygiene-tests.txt`.
- Source assertion: `git ls-files codex/bundles/skill-tool-mcp-isolation-template-migration` or equivalent post-repair proof.
- Anti-weakening audit: `rg -n "codex/bundles|tests/Memory|return true|Skip" Repository*HygieneTests.cs` style source check.

## Browser Validation Logging

- N/A. Backend/test-only repository hygiene.

## Progression Gate

- SB05 may not close until both hygiene tests pass and the execution report proves the scanner was not broadly disabled.

## Suggested Agent Prompt

```text
Implement SB01 only. Fix repository hygiene failures without weakening the guards. Capture failing-first and passing transcripts, then update the execution report.
```
