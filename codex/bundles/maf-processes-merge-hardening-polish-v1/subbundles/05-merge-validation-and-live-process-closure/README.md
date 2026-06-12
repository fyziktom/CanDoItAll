# Merge validation and live process closure

## Status

- `Ready`

## Objective

Run final merge-preparation validation, record honest closure evidence, and prove that the branch is ready to merge to `development` without losing the working multi-team app delivery behavior.

## Success Criteria

- Working tree has only intended source/test/docs changes.
- No tracked transient Codex work-package artifacts remain.
- No active bundle/SB/subbundle naming leaks remain.
- Process/driver unit tests pass.
- Process-filtered integration tests pass.
- Solution build passes.
- Existing successful multi-team app delivery evidence is preserved, or a fresh live smoke run is executed when environment and commands are available.
- Execution report is updated with exact commands, results, and any honest blocker.

## Covered Inputs

- User wants merge to `development` after hardening-polishing.
- User explicitly does not want drastic changes before merge.
- User stated multi-team app delivery truly worked and wrote a simple Tetris game based on project structure inputs.

## Prerequisites

- SB01-SB04 complete.

## Exact Source References

- Entire repo, but especially:
  - `CanDoItAll.slnx`
  - `src/CanDoItAll.Modules.Processes/**`
  - `src/CanDoItAll.Processes.Core/**`
  - `src/CanDoItAll.Processes.Drivers.*/**`
  - `src/CanDoItAll.AgentFramework.Maf/**`
  - `tests/CanDoItAll.Tests.Unit/**`
  - `tests/CanDoItAll.Tests.Integration/**`
  - `Templates/Processes/**`

## Deliverables

- Final execution report update in `reviews/01-execution-report.md` for this bundle, not a root repo report.
- A concise merge-readiness summary.
- No new repo-root Codex execution report file.

## Dependency Impact

- This closes the bundle and unlocks merge preparation.

## Validation Depth

- End-to-end regression and closure.

## Implementation Steps

1. Record `git status --short`.
2. Run final repository scans:

```bash
git ls-files | rg '(^01-execution-report\.md$|^codex/(bundles|bundle-exports)/|^codex/.*\.zip$)'
rg -n 'SB[0-9]{2,3}(_|-)?INV|SB[0-9]{2,3}|subbundle|bundle-exports|maf-processes-provider-hardening-followup|process-runtime-live-openai-verification-host-alpha' tests src Templates docs README.md --glob '!codex/skills/**'
rg -n '(Blazor|Razor|dotnet|\.csproj|\.slnx|npm|pnpm|yarn|vite|react|vue|svelte|javascript|typescript)' src/CanDoItAll.Modules.Processes/Automation/Dispatch --glob '!**/Domain/SoftwareDelivery/**' --glob '!**/ProcessSoftwareDeliveryEvidenceAdapter*.cs'
```

3. Run focused tests:

```bash
dotnet test tests/CanDoItAll.Tests.Unit --filter "Process|Driver|AgentRuntimeHardeningStaticRegression|SecretScanning|Repository"
dotnet test tests/CanDoItAll.Tests.Integration --filter Process
```

4. Run solution build:

```bash
dotnet build CanDoItAll.slnx --no-restore
```

5. Live smoke handling:
   - First search the repo for the exact current command used to run the multi-team app delivery process or the Tetris/TetrisGame scenario. Do not invent a command.
   - If a documented command exists and required environment variables are present, run one merge-smoke scenario with a small request that exercises project-structure grounding and multi-team app delivery.
   - If a fresh live run is too expensive or environment is unavailable, preserve the latest successful evidence by referencing the exact prior run artifact/log already in the repo or execution environment, and record why a fresh run was not performed.
   - Do not create a root `01-execution-report.md`; put proof under this bundle execution report or ignored local evidence path.
6. Update `reviews/01-execution-report.md` with command outcomes, scan outputs, and residual risks.
7. End with merge-readiness decision:
   - `Ready to merge`,
   - `Ready with explicit residual risk`, or
   - `Not ready`, with exact blockers.

## Scope Exceptions

- Do not run a full expensive live OpenAI process if the required environment is missing or command is not discoverable. Record the blocker honestly.
- Do not create tracked proof ZIPs or root execution reports.

## Do Not Do

- Do not hide failing tests by changing filters.
- Do not add skips unless each skip has owner, reason, reopen trigger, and replacement guard.
- Do not claim live process proof if no live process ran and no existing evidence was verified.
- Do not merge the branch in this subbundle unless explicitly instructed after closure.

## Acceptance Checklist

- [ ] Forbidden tracked artifact scan clean.
- [ ] Work-package naming scan clean.
- [ ] Generic dispatcher stack-specific term scan clean or only allowed domain adapter paths match.
- [ ] Process/driver unit tests pass.
- [ ] Process-filtered integration tests pass.
- [ ] Solution build passes.
- [ ] Live smoke evidence is present or blocker is explicit.
- [ ] Merge-readiness decision recorded.

## Proof Required

- All command transcripts listed in Implementation Steps.
- Execution report filled with pass/fail statuses.
- Any live smoke run ID/log path or blocker note.

## Browser Validation Logging

- N/A unless live smoke includes browser-visible app proof. If live smoke opens generated app UI, capture a maximized large-screen screenshot and record path.

## Progression Gate

Bundle closes only when final validation evidence supports an honest merge-readiness decision.

## Suggested Agent Prompt

```text
Implement subbundle 05 only. Run final merge-readiness scans, focused tests, integration tests, and build. Reuse the current documented live multi-team app delivery command if available and environment is present; otherwise record the exact blocker and preserve verified prior evidence. Do not create tracked root execution reports or proof ZIPs. Produce an honest merge-readiness decision.
```
