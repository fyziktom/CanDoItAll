# SB08 proof manifest

Status: Completed

Semantic invariant contract: `bundle://proof/SB08/semantic-invariants.md`

## Summary

- Ran the final targeted unit, integration, component, source assertion, CI metadata, and solution build regression matrix.
- Added the MAF executor binding ADR required by R10.
- Updated final architecture review, raw-note closure, and bundle status.
- Browser validation is represented by SB07 because SB08 made no UI changes.

## Proof

Hash sample: `168ceb71b6c327be469bf6b32d61ba5a82ee9ee4855dc5348e9620330dfe0905`.

- `bundle://proof/SB08/transcripts/unit-targeted-regression.txt`
  - Command: targeted unit filter covering package baseline, workflow templates/foundation/catalog, executor policy observability, event normalization, plugin manifest validation, and hosting DI.
  - Result: 60 passed, 0 failed, 0 skipped.
- `bundle://proof/SB08/transcripts/integration-targeted-regression.txt`
  - Command: targeted integration filter covering workflow API, plugin catalog/governance, and database migration checks.
  - Result: 40 passed, 0 failed, 0 skipped.
- `bundle://proof/SB08/transcripts/component-targeted-regression.txt`
  - Command: `WorkflowsPageTests`.
  - Result: 14 passed, 0 failed, 0 skipped.
- `bundle://proof/SB08/transcripts/final-build.txt`
  - Command: `dotnet build CanDoItAll.slnx --no-restore`
  - Result: build passed with 0 errors and existing EF Core Relational assembly-version warnings.
- `bundle://proof/SB08/transcripts/source-assertions-risky-invariants.txt`
  - Command: focused `rg` source assertion across MAF package usage, HITL, approval, event payloads, checkpoints, payload policy, observer composition, backend availability, deterministic test mode, in-process defaults, and workflow editor selector.
  - Result: expected source assertions found.
- `bundle://proof/SB08/transcripts/ci-metadata-check.txt`
  - Command: local CI metadata check.
  - Result: `.github` exists but `.github/workflows` does not; expected gate documented as local restore/build plus targeted unit/integration/component regression.
- `bundle://proof/SB08/final-verifier-red-team.md`
  - Result: adversarial closure audit passed for R1-R12 with residual follow-up triggers recorded.
- Failing-first: N/A for this process-only final regression and evidence cleanup; behavior-changing subbundles captured failing-first proof in SB01-SB07.
- Passing transcript: `bundle://proof/SB08/transcripts/unit-targeted-regression.txt`
- Anti-stub transcript: `bundle://proof/SB08/transcripts/anti-stub-audit-final.txt`
- Semantic invariant index: `bundle://proof/SB08/transcripts/semantic-invariant-evidence.txt`
- `bundle://proof/SB07/browser-workflow-runtime-backends.json`
  - Result: SB07 UI proof shows planned durable backends disabled and explained.
- `bundle://proof/SB08/transcripts/git-diff-check-final.txt`
  - Command: `git diff --check`
  - Result: passed with line-ending normalization warnings only.
- `bundle://proof/SB08/completed-bundle-validator.txt`
  - Command: completed-stage bundle validator.
  - Result: bundle is valid for stage `completed`.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Proof |
| --- | --- | --- | --- | --- |
| Regression transcripts | `dotnet test` and `dotnet build` | bundle closure review | Generated after SB01-SB07 are complete. | `bundle://proof/SB08/transcripts/unit-targeted-regression.txt`; `bundle://proof/SB08/transcripts/integration-targeted-regression.txt`; `bundle://proof/SB08/transcripts/component-targeted-regression.txt`; `bundle://proof/SB08/transcripts/final-build.txt` |
| MAF executor binding ADR | SB08 architecture cleanup | future runtime maintainers | Accepted as the explicit R10 strategy until benchmark/AOT evidence changes it. | `bundle://architecture/03-maf-executor-binding-decision.md`; `final-verifier-red-team.md` |
| Final architecture review | SB08 closure | bundle owner and future implementers | Summarizes shipped behavior and residual risks. | `bundle://reviews/02-final-architecture-review.md` |
| Browser validation analytics | SB07 browser proof, SB08 execution report | workflow UI maintainers | Captured only for UI-affecting subbundle. | `bundle://proof/SB07/browser-workflow-runtime-backends.json`; `bundle://reviews/01-execution-report.md` |

## Skipped

- Live Gmail, Office365, Docker, host-command, DurableTask, and Azure Functions execution proof was not run. Those paths remain disabled or unavailable by default and are covered by deterministic fake-mode and backend-unavailability tests.
