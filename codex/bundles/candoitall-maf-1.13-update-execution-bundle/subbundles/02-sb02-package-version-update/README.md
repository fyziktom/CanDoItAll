# SB02 Package Version Update

## Status

Completed.

## Objective

Apply only the package reference changes needed for the conservative MAF 1.13 update and dependency-floor alignment.

## Covered Inputs

- `bundle://inputs/original-prep/docs/02-nuget-update-inventory.md`
- `bundle://analysis/01-current-state.md`
- `bundle://inventories/01-scope-inventory.md`

## Prerequisites

- `SB01` baseline is complete.
- Baseline package and failure inventory exists.
- Working tree changes from unrelated user work are understood and not overwritten.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj`
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj`
- `bundle://inputs/original-prep/data/package-update-matrix.json`

## Deliverables

- Package-only diff for stable MAF packages.
- Preview A2A decision based on current NuGet CLI.
- Mem0 decision based on current NuGet CLI.
- Dependency-floor package decisions with restore/build evidence.
- Restore transcript.

## Dependency Impact

- `SB03` depends on the exact package graph produced here.
- `SB04` uses this subbundle to confirm unrelated packages were not updated.

## Validation Depth

- Package matrix proof.
- `dotnet list package --outdated --include-prerelease` proof.
- Restore proof.
- Source scan proof for stable MAF 1.8 references after edits.

## Implementation Steps

1. Re-run NuGet CLI outdated checks for MAF, Hosting, Workflow adapter, and Tooling projects.
2. Update stable MAF references to `1.13.0`.
3. Update `Microsoft.Extensions.AI.Abstractions` and `Microsoft.Extensions.DependencyInjection.Abstractions` only where required by MAF 1.13 restore/build.
4. Update A2A preview packages only if current CLI still confirms a compatible 1.13 preview.
5. Do not guess a Mem0 package version; keep or isolate only if restore/build proves a concrete issue.
6. Run `dotnet restore CanDoItAll.slnx`.
7. Record package diff and package decision table.

## Scope Exceptions

- `ModelContextProtocol`, `OpenTelemetry.Api`, `OllamaSharp`, `Azure.AI.OpenAI`, and unrelated packages stay unchanged unless restore proves unavoidable.
- `Microsoft.Extensions.*` latest versions are not automatically adopted.

## Do Not Do

- Do not edit application code.
- Do not introduce central package management.
- Do not update package families outside the decision table.
- Do not suppress warnings broadly.

## Acceptance Checklist

- Stable MAF packages are updated to `1.13.0` where targeted.
- A2A/Mem0 decisions cite current CLI output.
- Restore succeeds or failure is a clear package-only blocker.
- No application source files changed.
- No unrelated package references changed.

## Proof Required

- `proof/SB02/manifest.md`.
- `proof/SB02/semantic-invariants.md`.
- NuGet CLI transcript.
- Restore transcript.
- Package before/after table.
- Source scan transcript for stale stable MAF 1.8 references.
- Anti-stub audit transcript.

## Browser Validation Logging

- N/A for `SB02`; no browser-visible behavior changes are allowed.

## Progression Gate

- `SB03` may start only if restore succeeds or the remaining failure is explicitly a package-compatibility compile issue owned by `SB03`.

## C# Architecture Impact

- Package graph affects compile-time boundaries but should not change source architecture.

## Boundary Ownership

- Package references remain in owning adapter/hosting/tooling projects.

## Dependency Direction

- No new project references are allowed.

## Pattern Decision

- Package decision gate only; no production design pattern.

## Testability Contract

- Restore/build evidence validates package graph before code changes.

## Partial Class Policy

- No partial class changes allowed.

## Architecture Proof Required

- Package-only diff.
- No new central package management.
- No project-reference changes.

## Suggested Agent Prompt

Execute `SB02` only. Update the package references allowed by this README, using current NuGet CLI evidence for previews and dependency floors. Run restore and record proof. Do not edit application code.
