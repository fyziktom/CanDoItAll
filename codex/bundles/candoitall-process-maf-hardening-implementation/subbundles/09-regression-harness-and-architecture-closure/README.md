# SB09 - Regression Harness And Architecture Closure

## Status

- `Completed`
- Critical foundation: yes

## Objective

Prove the entire failure class is fixed with deterministic tests, recovery diagnostics, bundle closure evidence, CodeAnalytics refresh, and C# architecture gate review.

## Covered Inputs

- All GPTPro findings F01-F12.
- R01-R15.
- GPTPro B07 and acceptance criteria.
- User request to cover the live blocked 5032 example and similar template/artifact issues.

## Prerequisites

- SB01-SB08 closed with proof.

## Exact Source References

- `repo://tests`
- `repo://codex/bundles/candoitall-process-maf-hardening-implementation/proof`
- `repo://codex/bundles/candoitall-process-maf-hardening-implementation/reviews/01-execution-report.md`
- `repo://codex/bundles/candoitall-process-maf-hardening-implementation/traceability/01-requirement-traceability.md`

## Deliverables

- Consolidated regression harness covering observation, diagnostics, summaries, bridge, artifacts, preflight, templates.
- Current blocked-run recovery playbook for 5032 instance or explicit access blocker.
- Final CodeAnalytics snapshot and dependency/cycle proof.
- Final C# architecture review gate.
- Raw finding closure matrix with `Solved`, `Partially solved`, or `Not solved`.
- Completed proof manifests and semantic invariant contracts for all critical subbundles.
- Final validator pass.

## Dependency Impact

- This is the final closure phase. It cannot start until all foundations close.

## Validation Depth

- End-to-end regression and closure.
- Requires fake-proof resistance review.

## Implementation Steps

1. Run all targeted tests added by SB02-SB08.
2. Run relevant broader `dotnet test` and build commands based on changed projects.
3. Add no-live-LLM integration tests for subprocess parent/child state machine.
4. Add template validation over all process templates.
5. Add anti-stub audit across changed production files.
6. Refresh CodeAnalytics snapshot and dependency cycles.
7. Inspect live 5032 blocked process if tools/access are available; otherwise document blocker and validate recovery playbook locally.
8. Re-read GPTPro F01-F12 and record closure status.
9. Run `validate_bundle.py --stage completed`.

## Scope Exceptions

- Live 5032 recovery may be blocked by environment/tool access. If blocked, record exact missing access and still prove deterministic local coverage.

## Do Not Do

- Do not close from passing happy-path tests only.
- Do not accept populated tables, file existence, or child folder existence as final proof.
- Do not skip CodeAnalytics refresh when source architecture changed.
- Do not mark raw findings solved without evidence.

## Acceptance Checklist

- [ ] Observation truncation regression passes.
- [ ] Runtime receipt fallback regression passes.
- [ ] Structured result summary tests pass.
- [ ] Parent bridge active/accepted/repaired/no-go/missing-output tests pass.
- [ ] Artifact descriptor/content hash/ledger tests pass.
- [ ] Tool preflight missing/denied/available tests pass.
- [ ] Template validation across all process templates passes.
- [ ] CodeAnalytics cycles remain empty.
- [ ] C# architecture gate passes or blockers are explicit.
- [ ] Final bundle validator passes.

## Proof Required

- `proof/SB09/manifest.md`
- `proof/SB09/semantic-invariants.md`
- Final command transcripts.
- Final CodeAnalytics snapshot id and dependency result.
- Red-team fake-proof resistance artifact.
- Raw finding closure table.
- Changed-file hashes for all touched source/test/template/bundle files.
- Anti-stub audit output.
- Production Behavior Artifact Matrix for final production signals/records/events.

## Browser Validation Logging

- `N/A` unless live operator UI validation is performed.
- If UI is used, capture maximized desktop viewport and narrower viewport only if layout changed; record route/window, actions, assertions, screenshot paths, and result.

## Progression Gate

- Final closure only after completed-stage validator and architecture gate pass, or explicit blockers are recorded.

## C# Architecture Impact

Final architecture audit and proof consolidation.

## Boundary Ownership

Confirm all new behavior ended in the owners defined by architecture files.

## Dependency Direction

Refresh CodeAnalytics and verify no cycles or forbidden references.

## Pattern Decision

Validate pattern records against implementation and record deviations.

## Testability Contract

Confirm extracted services have direct tests and no unit proof depends on live LLM/network/full app host.

## Partial Class Policy

Confirm no new partial class is the final boundary and old large owners shrank or became thin delegates where touched.

## Architecture Proof Required

- Final `reviews/csharp-architecture-gate.md`.
- CodeAnalytics snapshot and cycle result.
- Changed-file source assertions.
- Testability proof.

## Suggested Agent Prompt

```text
Execute SB09 only after SB01-SB08 are closed. Consolidate regression proof, run final validators, refresh CodeAnalytics, perform C# architecture gate, and close every GPTPro finding note by note. Do not mark live 5032 recovery solved without direct proof or an explicit access blocker.
```
