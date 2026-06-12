# Structured Input

## Core Objective

Prepare `maf-processes-refactor` for merge to `development` by removing transient work-package artifacts and naming leaks, tightening process/driver boundaries, and preserving the working multi-team app delivery behavior.

## Success Criteria

- No tracked `codex/bundles/*`, `codex/bundle-exports/*`, root execution report, or transient ZIP artifacts remain in the branch.
- No active test method names or active source/test/docs/templates contain bundle/subbundle/SB/INV naming that came from Codex work-package plans, except under `codex/skills/bundles/**` where the bundle skill itself is source-of-truth tooling.
- MAF remains compile-time independent of `CanDoItAll.Modules.Processes`.
- Process Core remains deterministic and dependency-clean.
- Software-delivery proof/runnable-app/.NET/JavaScript/Blazor heuristics are owned by a domain driver or explicit domain adapter seam, not by the generic dispatcher runtime surface.
- Process driver gateway remains explicit, typed, read-only, mutation-free, and non-discoverable.
- Existing multi-team app delivery process behavior remains intact.

## Hard Constraints

- No drastic dispatcher-runtime refactor before merge.
- No dynamic driver host, registry, selector, DI discovery, manager command, scheduler hook, workflow hook, shell execution, Graph call, workspace/storage write, process-state mutation, finalizer mutation, transition mutation, or retry mutation in driver packages.
- No new MAF dependency on Processes.
- No broad UI changes.
- No hidden skips or report-only proof.

## Allowed Side Effects

- Delete transient helper artifacts from the repo.
- Tighten `.gitignore` for transient Codex work-package outputs.
- Rename tests and update assertions to semantic names.
- Add repository hygiene tests based on tracked files.
- Add or adjust driver boundary tests.
- Add a small verification-only software-delivery domain driver/adapter seam if needed to move domain proof policy out of the generic dispatcher.
- Update `CanDoItAll.slnx` and project references only for the new verification-only driver package if that route is used.

## Source Artifacts

- See `inputs/01-source-artifacts.md`.

## Input Coverage Signals

- The user explicitly called out leaks of bundle naming in tests as the first concern.
- The user explicitly stated bundles are development helpers and not a concern that should stay in the repo.
- The user explicitly asked whether domain drivers contain all necessary domain-related logic or whether items remain in the generic dispatcher runtime.
- The user explicitly stated dispatcher-runtime isolation is not the current goal and should start after merge.

## Dependency And Sequencing Signals

- Artifact hygiene must happen before final scans and validation.
- Test naming cleanup must happen before repository guardrails are treated as authoritative.
- Domain extraction must happen before final driver/gateway boundary tests are closed.
- Final validation must run after all previous subbundles.

## Validation Expectations

- Use source scans and tests, not bundle self-reporting.
- Favor tracked-file scans via `git ls-files` for repo artifact hygiene so local ignored Codex helper files do not break developer tests.
- Keep local transient files excluded from broad content scans, but reject tracked transient files.

## Evidence Contract

Required evidence in `reviews/01-execution-report.md` after execution:

- `git status --short`
- tracked artifact scan output
- bundle/SB naming scan output
- MAF/process reference scan output
- driver package boundary scan output
- process/driver focused unit test output
- process-filtered integration test output
- solution build output
- live multi-team process smoke evidence or explicit environment-blocked note with command attempted and failure reason

## UI Validation Strategy

N/A unless a subbundle touches UI. If UI is touched unexpectedly, run a maximized large-screen Playwright pass and attach screenshots.

## Browser Validation Analytics

N/A unless UI is touched. Do not invent browser proof for source-only cleanup.

## Working Assumptions

- Codex will execute on `maf-processes-refactor`.
- Existing local transient bundle directories may exist in Codex working directories; repository hygiene tests should focus on tracked files, not arbitrary ignored local files.
- The Tetris multi-team app delivery run already proved the branch can run the process; final validation should preserve this and rerun a smoke path only when environment and command are available.

## Primary Risks

- Removing too much under `codex/` could accidentally delete `codex/skills/bundles/**`, which is required tooling.
- Moving software-delivery proof rules too aggressively could break the working process run.
- A new domain driver could become a runtime host by accident.
- Scans that search all files instead of tracked files may fail locally because ignored Codex helper outputs exist.
- Keeping SB/bundle names in tests would make temporary planning language permanent architecture.
