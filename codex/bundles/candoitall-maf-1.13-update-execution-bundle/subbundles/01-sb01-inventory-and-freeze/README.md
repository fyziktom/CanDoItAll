# SB01 Inventory And Freeze

## Status

Completed.

## Objective

Capture the package, branch, source, build, test, and architecture baseline before any package file is edited.

## Covered Inputs

- `bundle://inputs/00-original-request.md`
- `bundle://inputs/original-prep/docs/01-current-architecture-map.md`
- `bundle://inputs/original-prep/docs/02-nuget-update-inventory.md`
- `bundle://analysis/01-current-state.md`

## Prerequisites

- Repository checkout is available.
- User has approved starting implementation in a later turn.
- No package reference or production code change has been made for this update yet.

## Exact Source References

- `repo://CanDoItAll.slnx`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj`
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj`
- `bundle://inputs/original-prep/docs/04-codex-execution-plan.md`

## Deliverables

- Baseline package list.
- Baseline git state.
- Baseline restore/build/test status.
- Existing warnings/failures separated from future package-update failures.
- Initial `docs/maf-1.13-update-evidence.md` skeleton if implementation starts.

## Dependency Impact

- `SB02` depends on this baseline to avoid mixing existing failures with package-induced failures.
- `SB05` depends on this baseline to interpret test failures honestly.

## Validation Depth

- Command-level proof.
- Package graph proof.
- Source scan proof for direct MAF references and process tool ambiguity.
- CodeAnalytics snapshot id or explicit unavailability note.

## Implementation Steps

1. Record `git status --short` and current branch.
2. Record `dotnet --info`.
3. List package references for all direct package-update-relevant projects.
4. Run direct package search across `src`, `tests`, and `tools`.
5. Run baseline restore and build if feasible.
6. Discover focused tests and record exact names.
7. Create proof directory `proof/SB01/` and store transcripts.

## Scope Exceptions

- Do not require all broad tests to pass before package update if baseline failures are already documented.
- Do not fix pre-existing vulnerabilities, warnings, or unrelated build issues in this subbundle.

## Do Not Do

- Do not edit package references.
- Do not edit production source.
- Do not normalize away existing warnings.
- Do not delete historical `processes_*` docs or tests.

## Acceptance Checklist

- Baseline package list exists.
- Branch and git state are recorded.
- Pre-existing restore/build/test failures are separated.
- Direct package reference inventory includes MAF, Hosting A2A, and Tooling dependency-floor references.
- Focused test candidates are recorded.
- `reviews/01-execution-report.md` has an `SB01` gate row update.

## Proof Required

- `proof/SB01/manifest.md`.
- `proof/SB01/semantic-invariants.md`.
- Command transcripts for git, package list, package search, and baseline restore/build attempts.
- Source assertion that no package files changed during `SB01`.
- Anti-stub audit transcript showing no placeholder evidence rows are marked complete.

## Browser Validation Logging

- N/A for `SB01`; no browser-visible behavior changes are allowed.

## Progression Gate

- `SB02` can start only when baseline evidence is captured and all unknown baseline failures are either explained or converted into blockers.

## C# Architecture Impact

- Establishes the current architecture facts used by later C# gates.

## Boundary Ownership

- MAF package ownership remains in MAF adapter projects.
- Process ownership remains in `Processes.*` and modules.

## Dependency Direction

- Record current references before any changes.

## Pattern Decision

- No production pattern is selected in `SB01`.

## Testability Contract

- Record existing focused tests and gaps.

## Partial Class Policy

- No partial class changes allowed.

## Architecture Proof Required

- CodeAnalytics snapshot id or unavailability note.
- Current package/project reference table.
- Current hotspot file inventory.

## Suggested Agent Prompt

Execute `SB01` only. Capture branch, git status, package list, package search, baseline restore/build, focused test candidates, and CodeAnalytics evidence. Do not edit source or package references. Update `reviews/01-execution-report.md` and create `proof/SB01/manifest.md`.
