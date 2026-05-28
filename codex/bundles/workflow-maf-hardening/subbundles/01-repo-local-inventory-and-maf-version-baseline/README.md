# 01-repo-local-inventory-and-maf-version-baseline

## Status

- `Prepared`

## Objective

Create the authoritative repo-local map of Agents/Workflows/Plugins and establish the MAF package/API baseline before any hardening edits.

## Success Criteria

- Current branch, commit, restore/build status, and SDK environment are recorded.
- All workflow-related source files are classified by responsibility.
- All MAF package references and versions are listed.
- A deliberate decision is recorded: upgrade all stable MAF packages to `1.7.0` now, or remain on `1.6.2` temporarily with reasons.
- Existing native MAF usage is classified: none/model-only/adapter/native executor/runtime/test.
- Plugin executor surfaces are identified.

## Covered Inputs

- R01, R02, R05, R07, R08, R09, R11, R13, R15

## Prerequisites

- Working tree on `processes-hardening`.
- Ability to run `git`, `dotnet`, and `rg`/PowerShell equivalent.

## Exact Source References

- `CanDoItAll.slnx`
- `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `src/CanDoItAll.Modules.AgentFramework/`
- `src/CanDoItAll.AgentFramework.Core/`
- `src/CanDoItAll.AgentFramework.Models/`
- `src/CanDoItAll.AgentFramework.Persistence/`
- `src/CanDoItAll.Modules.Plugins/`
- `src/CanDoItAll.Plugins.Abstractions/`
- `src/plugins/CanDoItAll.Plugin.*/*`
- `Templates/Workflows/`
- `tests/`

## Deliverables

- `inventories/02-local-source-inventory.md`
- `inventories/03-maf-version-baseline.md`
- `inventories/04-plugin-executor-inventory.md`
- Initial proof logs under `proof/SB01/`

## Implementation Steps

1. Record `git status --short`, `git rev-parse --abbrev-ref HEAD`, and `git rev-parse HEAD`.
2. Run source scan from `inventories/01-scope-inventory.md`.
3. Inspect every hit and classify it by responsibility.
4. List all `Microsoft.Agents.AI*` packages and project references.
5. Compare local MAF package versions with currently available NuGet versions.
6. Run restore/build if environment supports it.
7. Do not change runtime code yet, except optionally updating the bundle report files.
8. Update `reviews/01-execution-report.md`.

## Scope Exceptions

- Do not implement runtime/compiler/plugin changes in SB01.
- Do not perform package upgrades in SB01 unless the maintainer explicitly authorizes doing the package-bump proof as part of the baseline.

## Do Not Do

- Do not skip files because they are UI-facing; UI launch paths often hide runtime coupling.
- Do not assume plugin executors exist only because plugin projects exist. Find actual invocation paths.
- Do not treat an in-process preview runner as durable production proof.

## Acceptance Checklist

- Inventory files exist and are specific enough for later subbundles.
- MAF version decision is explicit.
- Build/restore/test blockers are documented with exact command output.
- No functional architecture edits were made.

## Proof Required

- `proof/SB01/transcripts/git-baseline.txt`
- `proof/SB01/transcripts/source-scan.txt`
- `proof/SB01/transcripts/restore-build.txt`
- `proof/SB01/maf-version-decision.md`

## Progression Gate

SB02 may start only after SB01 gives a clear map of current workflow model, runtime, plugin, UI, persistence, and test surfaces.

## Suggested Agent Prompt

```text
Implement SB01 only. Produce a rigorous inventory and MAF version baseline. Do not perform functional runtime edits. Capture proof and update the execution report.
```
