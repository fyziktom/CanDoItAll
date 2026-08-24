# SB12 — Final architecture/stable gate, clean multi-instance run, running handoff, and closure

State: `LOCKED`  
Proof tier: `Governed`  
Depends on: `SB11`  
Next on pass: `NONE`

## Objective

Freeze the implementation, run the one allowed stable aggregate and final clean three-instance lane, leave services running, and close every requirement with durable evidence.

## Observable outcome

The feature is implementation-complete, documented, externally described, manually testable on running containers, and auditably closed.

## Inputs and current-state anchors

- Bundle root execution contract and architecture documents.
- Current repository state, not only the prepared SHA.
- Relevant source/test impact maps.
- Completed proof and handoff from every dependency.
- Current mandatory SharedInfo skills.

## Scope

- Freeze production/test/docs/SharedInfo changes and capture final worktree state.
- Run final dependency/CodeAnalytics/C# architecture/security reviews.
- Build the final app/test graphs once.
- Run the stable aggregate exactly once with named invalidation triggers.
- Reset only dedicated E2E state, build/reuse final app image, and run final clean multi-instance scenario set.
- Verify central/client-a/client-b/PostgreSQL/upstream health after scenarios.
- Verify secret/content/log/database scans.
- Generate final manual handoff with URLs, fixtures, local credential file locations, logs and cleanup command.
- Do not execute cleanup or docker compose down.
- Update execution report, all proof manifests/STATUS, traceability matrix, reviews, and CLOSURE.md.
- Classify every FR/NFR and residual risk.
- Run bundle validator in final closure mode.

## Out of scope

- No feature expansion.
- No second stable run.
- No container cleanup.
- No live paid provider.

## Implementation sequence

1. Refuse late refactors unless they repair a blocking gate; reopen owner if required.
2. Record exact stable invalidation triggers and command.
3. Ensure final image tag matches final source/worktree.
4. Run all Docker scenarios from clean dedicated state.
5. After success, collect status without stopping services.
6. Write manual handoff from current running state.
7. Mark cleanup command NOT EXECUTED.
8. Verify OpenAPI/SharedInfo still match final product commit.
9. Close each requirement with direct artifact path.

## C# Architecture Impact

This subbundle is architecture-significant. Re-read
`architecture/00-csharp-current-state-inventory.md` through
`architecture/04-csharp-testability-plan.md`, update the affected checkpoint, and stop rather
than use a boundary workaround.

## Boundary Ownership

Closure/evidence only except repairs reopened to their owning subbundle.

## Dependency Direction

No new dependency change expected. Any change invalidates final freeze and requires re-review.

Record before and after `ProjectReference`/namespace direction even when no reference is
expected to change. A no-change result is still evidence.

## Pattern Decision

Frozen release gate and artifact-backed closure.

Do not introduce an adjacent alternative pattern without reopening the owning ADR and
recording why the selected pattern failed.

## Testability Contract

One stable aggregate plus one final clean black-box Docker lane.

Every new behavior needs one realistic positive proof and one meaningful negative proof. Test
existence, file counts, status codes alone, or mocked self-assertions do not prove behavior.

## Partial Class Policy

No new partials.

A large partial or monolithic file is a gate failure unless the architecture review documents
a narrow unavoidable reason.

## Architecture Proof Required

- Final before/after dependency graph and architecture gate.
- One stable aggregate transcript.
- Final scenario-results.json.
- Container health/status after test.
- Secret/content scan.
- OpenAPI/SharedInfo final consistency.
- Manual handoff.
- Complete requirements matrix and closure report.

## Test selection

| Topic | Owning project/lane | Stable filter | Planned expected discovery | Selection reason |
| --- | --- | --- | ---: | --- |
| `CanDoItAll stable aggregate` | `tests/Solutions/CanDoItAll.Tests.Stable.slnx` | `full stable graph at frozen checkpoint` | record frozen aggregate discovery before run | Single final broad gate justified by project, Web, auth, EF, middleware and OpenAPI changes. |
| `SharedProviderMultiInstanceE2E` | `tools/SharedProviders E2E orchestrator` | `scenario-set:final-clean` | 19 | Final clean real-system proof and leave-running handoff. |
| `BundleClosureValidation` | `bundle scripts and SharedInfo validators` | `closure, traceability, hashes, running handoff` | 6 | Ensures artifact-backed final closure. |

Before running a test topic:

1. build the owning production/test assembly;
2. run `--list-tests` when it is a .NET test lane;
3. compare actual discovery with the planned count;
4. update the planned count only before execution and with a written implementation-based
   reason;
5. reject zero discovery;
6. record transcript and counts in `proof/proof-manifest.json`.

Do not run an unfiltered project or broader lane unless this subbundle explicitly owns it.

## Acceptance criteria

- Every previous gate passes.
- Stable aggregate ran no more than once and passed.
- Final three-app lane passed from clean state.
- All required services remain running/healthy.
- Handoff contains no secret values.
- Every FR/NFR is closed or explicitly blocked/partial with evidence.

## Negative proof

- Command audit proves no extra broad/stable run.
- Cleanup/down was not executed.
- No tracked artifact contains credential or content canaries.
- No unsupported route/capability appears after final freeze.

## Semantic invariants

- Only one stable aggregate run.
- Final stack is left running.
- Closure, code, OpenAPI, SharedInfo and proof agree.

## Evidence artifacts

At minimum:

- completed `proof/proof-manifest.json`;
- command transcripts under `proof/transcripts/`;
- changed-file inventory;
- architecture/reference artifacts;
- focused behavior artifacts;
- completed `SESSION-HANDOFF.md`;
- updated root `STATUS.md` and traceability rows.

## Progression gate

Pass only when every acceptance criterion, architecture assertion, focused build/test, and
negative proof is backed by an artifact. On pass mark this subbundle `DONE`, unlock only
`NONE`, and update the owning review.

On failure, keep downstream work locked. Do not call a missing proof a residual risk.

## Reopen triggers

- Any final gate fails.
- Final image/source mismatch.
- OpenAPI/SharedInfo no longer match.
- Containers are not healthy/running.
- Traceability has an ambiguous requirement.

## Execution checklist

- [ ] Current branch/commit/worktree captured.
- [ ] Mandatory skills loaded.
- [ ] Bundle and subbundle readiness validated.
- [ ] Dependencies are `DONE`.
- [ ] Before architecture/reference evidence captured.
- [ ] Scope implemented without widening.
- [ ] Affected production projects built.
- [ ] Test discovery recorded and nonzero.
- [ ] Focused positive/negative tests passed.
- [ ] Security/redaction checks passed where applicable.
- [ ] After architecture/reference evidence captured.
- [ ] Proof manifest completed with artifact hashes.
- [ ] Session handoff completed.
- [ ] Status/traceability/review updated.
