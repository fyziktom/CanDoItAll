# Process Agent Contracts And Live Data

## Source Runs

- Process definition: `4fdc77a9-6d8c-4b10-9efb-4be15732b1b0`.
- Original operator project: `80b84f2c-8d8c-4f3c-8016-e7e05cccf2e1`.
- Original blocked implementation run: `3bdbfe3e-7562-4ecc-96e3-8faff16192be`.
- Earlier blocked run with recorded implementation artifacts: `cf086486-2424-487b-bd29-bfc3c111f307`.
- Generic validation run 1: `908bfd0f-4039-432e-914b-b8a7c35f17ae`, `Basic App patched chain validation 2`, completed on `2026-05-04T08:28:46Z`.
- Generic validation run 2: `ce0da97a-ece3-46ec-b0b2-c443271d8d8d`, `Harbor Shift Scheduler generic chain validation`, completed on `2026-05-04T10:17:12Z`.
- Active validation host: `https://localhost:7271` / `http://localhost:5032`, PostgreSQL workspace profile `dc8abe54-58cd-4a87-98ab-5a14de6f846b`.

## Live Failure Shape

- Original run `3bdbfe3e-7562-4ecc-96e3-8faff16192be` blocked at `Implement feature, tests, and migration notes` because the implementation agent claimed completion without current, concrete product-source/build/test proof.
- Original run `cf086486-2424-487b-bd29-bfc3c111f307` exposed the retry variant: required implementation artifacts were already recorded, but the dispatcher evaluated only the newest finalizer attempt and ignored reusable current-step proof/artifact records.
- The Basic App run proved that implementation and repair artifacts can now carry forward across retry/finalizer attempts and that the whole chain can close with a durable repair-escalation no-go when QA still finds release-blocking defects.
- The Harbor run exposed three additional live issues and now proves their repaired behavior:
  - Path grounding: prompts could include escaped `./nAll app source` suffixes that produced invalid external-target roots. The dispatcher now normalizes those annotations before product-root validation.
  - Browser artifacts: provider-native browser screenshots, snapshots, and console logs were real files but were not projected or recognized as durable process evidence. Browser MCP context is now bounded, image payloads are removed from model context, and provider-native browser outputs are projected/typed for process evidence.
  - Terminal escalation: the release-readiness agent blocked the `Escalate unresolved repair findings` step because the product was not release-ready, even though that step's job is to record the no-go decision. Escalation/no-go steps now instruct and normalize completion when the required decision artifact is written.
- Final targeted integration proof after these repairs passed 265 dispatch/runtime tests covering per-agent tool contracts, process-mock artifacts, hard failed tools, blocked proof gaps, provider failures, no-outcome failures, browser artifact discovery, repair branch routing, and terminal escalation completion.

## Version And Id Control

The live run uses snapshot step and artifact expectation ids that do not match the current exported working definition ids. Runtime validation and tests must therefore use ids from the run detail or step run context, not ids copied from the latest definition export.

## Agent Contract Matrix

| Step agent | Live step responsibility | Must receive | Must produce | Tool profile checks | Step completion gate |
| --- | --- | --- | --- | --- | --- |
| Product owner / Business Strategist | Clarify scope and release boundary | Requested change, target project node, stakeholder constraints, delivery assumptions | `Scope boundary packet` brief | Artifact read/write and finalizer; no workspace mutation required | Scope packet must be recorded against the live step expectation and reusable by architecture |
| Solution architect | Review architecture and canonical-model impact | Scope packet, project-structure context, touched modules, integration concerns | `Project structure context brief`; `Architecture decision record` | Project structure read tools, artifact read/write, finalizer; source read allowed | Both artifacts must be recorded; ADR must identify selected path, rejected options, source of truth, and migration ownership |
| Lead engineer / Blazor Application Developer | Implement feature, tests, and migration notes | Scope packet, ADR, project structure brief, resolved workspace root, current artifact records | `Implementation change set`; `Migration and rollout preparation checklist` | Workspace read/write/scaffold/build/test/run plus artifact read/write and finalizer | Required artifacts must be recorded, required tools must run or be satisfied by carried prior proof, and any fresh product mutation must invalidate stale proof until new source/build/test evidence exists |
| Lead engineer reviewer | Complete peer review and integration readiness | Implementation package, ADR, touched-surface inventory | `Peer review note` evidence | Source/artifact read and finalizer; mutation should be denied unless explicitly repairing review comments | Review note must capture accepted issues, rejected concerns, residual risk, and downstream QA implications |
| QA lead | Run QA validation and browser proof | Peer-reviewed change set, changed-surface inventory, release-scope assumptions | `Regression evidence pack` evidence and explicit branch outcome | Workspace validation/build/test/browser read tools; no scaffold or product mutation | Must select `Quality accepted` or `Repair required`; evidence must include test/browser proof or concrete proof gaps |
| Lead engineer repair | Repair validation findings | QA repair-required disposition, failing proof details, reviewed implementation package | `Quality repair change set` | Workspace read/write/build/test/run plus artifact read/write and finalizer | Repair must be scoped to QA findings and must rerun invalidated proof tools |
| QA lead repair validation | Re-run QA validation and browser proof after repair | Repair change set, original QA findings, reviewed implementation package | `Repaired regression evidence pack` and explicit branch outcome | Workspace validation/build/test/browser read tools; no scaffold or product mutation | Must select repaired quality accepted or repair escalation with proof |
| Security reviewer | Perform security and data-handling review | QA-accepted package, changed-surface inventory, data-handling notes | `Security exception assessment` decision | Source/dependency/config read, artifact write, finalizer; no product mutation | Assessment must name controls, residual risk owner, and approval/block rationale |
| Security reviewer after repair | Perform security review after repair | QA-accepted repaired package, repair notes, changed-surface inventory, data-handling notes | `Security exception assessment` decision | Source/dependency/config read, artifact write, finalizer; no product mutation | Same as first-pass security, with explicit repair impact |
| Delivery manager / release approver | Approve first-pass release readiness | QA evidence, security outcome, rollback plan, support ownership | `Release approval record` decision | Artifact read/write, decision finalizer; no workspace mutation | Approval must name approver, residual risk owner, rollback trigger, and timing conditions |
| Release manager | Execute first-pass controlled release rollout | Approved release record, deployment package, rollback plan, telemetry watch points | `Deployment and telemetry watch log` transcript | Release/telemetry tools where configured, artifact write, finalizer | Rollout transcript must capture timing, telemetry checkpoints, and halt/rollback status |
| Delivery manager learning | Capture first-pass post-release learning | Rollout outcome, telemetry record, support observations, incident notes | `Post-release learning review` decision | Artifact read/write and finalizer | Learning review must capture orchestration-quality observations and accountable follow-up actions |
| Delivery manager escalation | Escalate unresolved repair findings | Post-repair QA escalation, repair notes, remaining release-blocking evidence | `Repair escalation record` decision | Artifact read/write and decision finalizer | No-go, reset, or replan decision must name owner and next repair scope |
| Delivery manager / release approver after repair | Approve repaired release readiness | Repaired QA evidence, security outcome, rollback plan, support ownership | `Release approval record` decision | Artifact read/write, decision finalizer; no workspace mutation | Same as first-pass release approval, with explicit repair evidence |
| Release manager after repair | Execute repaired controlled release rollout | Approved repaired release record, deployment package, rollback plan, telemetry watch points | `Deployment and telemetry watch log` transcript | Release/telemetry tools where configured, artifact write, finalizer | Same as first-pass rollout, with repaired-package reference |
| Delivery manager learning after repair | Capture repaired-release learning | Rollout outcome, telemetry record, support observations, incident notes | `Post-release learning review` decision | Artifact read/write and finalizer | Same as first-pass learning, with repair-path observations |

## Live Agent-By-Agent Proof

| Step | Run proof | Inputs verified | Outputs verified | Result |
| --- | --- | --- | --- | --- |
| Scope | Harbor `3caef669-994a-41e5-8d96-e4e950480bdf` | Project/process/node context and requested delivery boundary | `01-scope-boundary-packet.md` | Completed |
| Architecture | Harbor `f9f7847a-99f5-420b-a93f-736737706789` | Scope packet, project-structure read, normalized external product root | `01-architecture-decision-record.md`; `02-project-structure-context-brief.md` | Completed after path-grounding repair |
| Implementation | Harbor `95d9dd8c-31f5-46c2-9e32-f9a7ceaf9c74`; Basic App `908bfd0f-4039-432e-914b-b8a7c35f17ae` | Scope packet, ADR, product root, workspace command profile | `03-implementation-change-set.md`; `03-migration-and-rollout-preparation-checklist.md` | Completed with carried proof/artifact validation |
| Peer review | Harbor `9214b946-64de-4a51-8df2-0d41297d0d28` | Implementation package and ADR | `04-peer-review-note.md` | Completed |
| QA | Harbor `4f8a29ce-0836-4bca-855e-e07dce352d78` | Implementation, peer review, browser/workspace tools | `regression-evidence-pack.md`; branch `Repair required` | Completed after repair-branch disposition fix |
| Repair | Harbor `bc9be4e9-a0fc-488b-849f-23c59dba2c1e` | QA defect evidence and implementation package | `06-quality-repair-change-set.md` | Completed |
| Re-run QA | Harbor `679145b6-63b7-4647-85bd-14c0917dff9b` | Repair change set, prior QA defects, browser/workspace tools | `repaired-regression-evidence-pack.md`; branch `Repair escalation` | Completed after artifact-record recovery and branch normalization |
| Escalation | Harbor `4ff3bd22-5f80-40cd-b090-d4abe78f851f` | Post-repair QA escalation, repair notes, remaining release-blocking evidence | `repair-escalation-record.md`, artifact record `610cda0d-462b-4b61-a666-e38fe4df7447` | Completed after terminal no-go contract repair |
| Security/release/rollout/learning | Harbor steps 7-11 and 13-15 | Branch dependencies from QA/repaired QA | No output expected because release path was not selected | Skipped by modeled branch gating |

## Durable Data Access

- Harbor scoped artifacts root: `artifacts/scopes/organization/dc8abe5458cd4a8798ab5a14de6f846b/process-runs/ce0da97a-ece3-46ec-b0b2-c443271d8d8d`.
- Harbor raw provider-native browser artifacts root: `artifacts/process-runs/ce0da97a-ece3-46ec-b0b2-c443271d8d8d`.
- Harbor final escalation artifact: `artifacts/scopes/organization/dc8abe5458cd4a8798ab5a14de6f846b/process-runs/ce0da97a-ece3-46ec-b0b2-c443271d8d8d/repair-escalation-record.md`.
- Harbor final run status from `Processes_Runs`: `Completed`, `CompletedAtUtc=2026-05-04T10:17:12Z`, blocked step count from run detail `0`.
- Basic App final run status from `Processes_Runs`: `Completed`, `CompletedAtUtc=2026-05-04T08:28:46Z`.
- Final targeted integration command: `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~MafAgentRuntimeTests" --artifacts-path C:\repositories\CanDoItAll\.codex-tmp\artifacts-proof-final-integration8 --logger "console;verbosity=minimal"`; result `Passed`, 265 tests.

## Step-By-Step Test Requirements

1. Each step-agent must be exercised with a work brief, input artifacts, output artifact expectations, and effective tool profile matching the live run shape.
2. The implementation agent must be tested in three separate modes:
   - Fresh implementation with no prior artifacts.
   - Finalizer-only retry after valid source/build/test proof and required artifacts.
   - Fresh product mutation after prior proof, which must require new source/build/test evidence before completion.
3. Artifact validation must prove that current-step `ProcessArtifactRecord` entries satisfy the live run expectation ids.
4. Proof validation must prove that prior implementation proof is reusable only when no newer concrete product mutation exists.
5. The whole chain must be tested with at least two different generated-app topics so process behavior is generic and not tied to the original unit-conversion app.

## Requirement Closure

- Requirement 1 is covered by the live Harbor step-run sequence and the contract matrix above.
- Requirement 2 is covered by targeted dispatch tests for fresh implementation, finalizer-only retry, and fresh product-mutation invalidation.
- Requirement 3 is covered by process detail for Harbor and Basic App runs plus artifact record `610cda0d-462b-4b61-a666-e38fe4df7447`.
- Requirement 4 is covered by targeted dispatch/runtime tests, including the final 265-test pass, and the completed implementation steps in both validation topics.
- Requirement 5 is covered by the completed Basic App and Harbor Shift Scheduler runs.
