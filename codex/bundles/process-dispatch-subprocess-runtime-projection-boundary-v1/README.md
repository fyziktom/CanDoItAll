# process-dispatch-subprocess-runtime-projection-boundary-v1

Status: Prepared for Codex implementation.

## Mission

Continue the `maf-processes-refactor` sequence with a **module-local subprocess runtime/projection boundary**. This bundle intentionally does **not** create `CanDoItAll.Processes.Core` and does **not** create production process driver APIs.

The current branch has already isolated MAF/process tool providers, execution snapshots, artifact projection/write coordinators, artifact validation rules, tool validation/recovery helpers, finalizer helper partials, route/candidate hydration, candidate factory/cooperation metadata, and pre-execution materialization guards. The next high-value seam is the subprocess branch inside `ProcessRunAutomationDispatchService.Dispatch.cs`.

## Target seam

Current subprocess-related responsibilities still grouped in `Dispatch.cs` include:

- subprocess start transition
- `EnsureSubprocessRunForStepAsync` call through `ProcessesService`
- terminal status mapping and parent transition reason
- capability gap child-step query and block reason
- completed subprocess artifact projection
- subprocess projection gap fingerprint/journal
- parent-scoped markdown file write
- parent artifact record and artifact-recorded journal write
- finalizer context creation and transition application

This bundle extracts those into module-local helpers and/or focused partials with parity proof.

## Hard constraints

- No Process Core project, namespace, or production source.
- No production process driver APIs, driver registries, driver packages, or `IProcessDriverPack`.
- No MAF back-dependency or Tooling dependency broadening.
- No UI/Razor/CSS/JS/TS changes.
- Browser validation is `N/A` for all subbundles unless UI unexpectedly changes; if that happens, use large desktop/PC proof only.
- Keep subprocess side effects explicit. Do not hide EF writes or file writes inside pure-looking planners.
- Preserve all existing public/internal behavior and wrappers unless a subbundle explicitly proves a safe internal move.

## Subbundle count

24 subbundles with refactor gates at SB04, SB08, SB16, SB19, SB23, and SB24.
