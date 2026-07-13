# SB01 Re-entry Package And Baseline Gate

## Status

- `Completed`

## Objective

- Re-anchor execution on current commits and produce a reproducible, validated FileTools package baseline before any main-app product edit.

## Covered Inputs

- N001-N005, N012-N014; R001-R002, R008, R024, R026, R029-R030.

## Prerequisites

- Prepared bundle validator and manual readiness Pass.
- Clean understanding of user-owned worktree changes; do not overwrite them.

## Exact Source References

- `repo://global.json`
- `repo://NuGet.Config`
- `repo://ExternalPackages`
- `repo://CanDoItAll.slnx`
- `C:\repositories\CanDoItAll.FileTools\global.json`
- `C:\repositories\CanDoItAll.FileTools\CanDoItAll.FileTools.slnx`
- `C:\repositories\CanDoItAll.FileTools\Directory.Build.props`
- `C:\repositories\CanDoItAll.FileTools\scripts\pack-release.ps1`
- `C:\repositories\CanDoItAll.FileTools\scripts\validate-packages.ps1`
- `C:\repositories\CanDoItAll.FileTools\.github\workflows\ci.yml`
- `bundle://inputs/01-source-artifacts.md`.

## Deliverables

- Fresh branch/commit/status record for both repos and a changed-file ownership audit.
- SDK `10.0.301` available as declared by FileTools. If it cannot be provisioned, mark SB01 Blocked; do not edit the pin silently.
- Current FileTools restore, Release warnings-as-errors build, tests, format, pack, package validation, exact IDs/version, nupkg/snupkg SHA-256.
- Fresh focused FileTools CodeAnalytics snapshot with health/dependency/cycle result.
- Main baseline restore/build/targeted tests status and scoped CodeAnalytics refresh.
- Components MCP libraries/recommendations probe and shared dotnetwatch/workspace availability probe; retry once after transport restart. UI remains blocked if unavailable.
- No package copied to main yet; SB06 owns intake after provenance is proven.

## Dependency Impact

- SB02-SB18 depend on current source/package/API evidence. Weak provenance invalidates every package/reference/UI proof.

## Validation Depth

- Proof tier: `Standard`.
- Critical foundation for source and package reproducibility.

## Implementation Steps

1. Read bundle roots/status and compare current commits to preparation pins.
2. Capture status without cleaning/resetting user changes.
3. Provision exact FileTools SDK or stop Blocked.
4. Run FileTools documented validation and package scripts; record hashes.
5. Build CodeAnalytics snapshots and inspect dashboard/dependencies, not empty results.
6. Run main baseline checks proportionate to affected future projects.
7. Call Components MCP and dotnetwatch workspace/status discovery; record tool gaps.
8. Update execution report/SB01 gate immediately.

## Scope Exceptions

- No main production code, main package intake, component choice, or UI work.
- A two-file FileTools culture-stability repair was required because the documented package pipeline failed outside an English locale. The repair and its regression test are recorded in `bundle://proof/SB01/baseline.md`; it is part of the current package provenance.

## Do Not Do

- Do not update FileTools `global.json`, publish packages, copy unvalidated packages, or treat old bundle transcripts as current proof.

## Acceptance Checklist

- [x] Source pins/status and user changes are recorded.
- [x] FileTools validation/pack/hash proof passes on current source.
- [x] FileTools snapshot is non-empty and usable.
- [x] Main baseline is recorded without hiding failures.
- [x] Components/watch availability is known.
- [x] Main product diff remained empty during SB01; the required FileTools baseline repair is explicit and hashed.

## Proof Required

- Exact commands/exit codes under `bundle://proof/SB01/transcripts/` or Standard execution-report records.
- Package ID/version/hash table and CodeAnalytics snapshot IDs.
- `git diff --name-only` proving no product edit.

## Browser Validation Logging

- N/A; tool readiness only, no rendered product behavior.

## Progression Gate

- SB02 may start only with current source and validated FileTools packages. UI phases additionally require Components MCP and managed watch/browser availability.

## Reopen Triggers

- FileTools commit/package/API/hash drift, SDK change, package validation failure, or stale/empty snapshot reopens SB01 and SB06+ proof.

## Suggested Agent Prompt

```text
Establish the exact reproducible package and repository baseline for SB01 only. Make no product edits. Stop rather than rewriting SDK pins or trusting stale proof. Record current commands, hashes, snapshots, tool availability, and the progression decision.
```
