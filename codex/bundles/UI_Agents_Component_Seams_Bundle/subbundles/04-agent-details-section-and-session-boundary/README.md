# SB04 — Editor section, session and host lifetime

Status: **Not started**. Proof tier: **Behavioral**. Current authorization is documentation only.

## Objective and covered inputs

Expose semantic section and explicit per-instance session ownership without losing current editor presentation or draft state.

R-023–R-026/R-034/R-036/R-039/R-043/R-045/R-052/R-053; F02/F04/F05/F08; B09–B12/B16/B19–B28, with command details completed in SB05. See [requirements](../../requirements/00-normalized-requirements.md), [behavior matrix](../../requirements/02-behavior-preservation-matrix.md) and [accepted revision](../../inputs/03-accepted-review-and-revision-request.md).

## Prerequisites and exact source references

SB03 accepted; B12 baseline characterized, reset/target/close oracles recorded; subtree and exact section tests selected.

src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor and .razor.cs; host opening call sites in AgentsHomePage/catalog coordination; same-module session/section/load types; real child paths in subtree inventory; six AgentDetailsDialog*Tests families and shared harness.

## Scope and deliverables

Typed section mapping; explicit existing/create target and instance/generation; normal production load seam; independent references; host transition/result contract; migrated public section/loading tests and real child scenarios.

## Implementation steps for later authorized execution

1. Implement the session/host contract with one mutable draft/edit context owner and preserved expected token. Do not retain sessions in circuit-scoped services.
2. Map ten section identities to existing Tabs API/order; same-target section change must retain draft. Preserve Clear-to-blank and synchronize create target without conflating catalog selection.
3. Load through the production operation seam, preserve provider/secret partial errors and explicit project loading. Treat characterized B12 behavior accurately.
4. Inventory and render conditional children/nested dialogs with their real fakeable capabilities; record exact required same-module child edits for SB05 and external graph blockers.
5. Migrate details harness/section tests progressively and prove delayed old success/failure, reset/new target/dispose, and two instance isolation.

## Dependency impact and do-not-do constraints

No global DialogService behavior change, routed overlay host, new URL or sibling API edit. InitialSession is optional only for justified real production composition with defined ownership/precedence.

Apply the [invariants](../../requirements/01-invariants-and-non-goals.md), [pattern decisions](../../architecture/03-csharp-pattern-selection-records.md), [UI composition contract](../../architecture/10-ui-composition.md) and [recovery/invalidation rules](../../plan/01-dependencies-reopen-and-invalidation.md). Do not start later phases on incomplete required proof.

## Validation depth and acceptance

Named load/section/lifetime/component cases plus affected existing details settings/thinking/project/avatar tests. Required real descendants use deterministic services; do not suppress them to prove a section. Freeze exact tests/data/counts and run current catalog->editor host dependent flow. [Shared commands](../../commands/00-validation-commands.md) define reusable selectors; phase proof records the exact selected names/data cases and expected count before source edits, then actual discovery/results.

- [ ] All ten sections use stable semantic identity and preserve existing controls/labels.
- [ ] Session/draft/edit context/reference ownership and create/reset transitions are explicit and tested.
- [ ] No shared mutable editor service or default test-only session shortcut; pending results cannot replace a newer/disposed session.
- [ ] Core-load behavior is characterized; partial/lazy reference semantics and real-child coverage are recorded.

## Proof and progression gate

Session transition evidence, selected case discovery/transcripts, delayed success/failure tests, host/section/UI composition proof, child registration matrix and updated dependency audit. Store execution artifacts under proof/SB04; follow [proof placement](../../proof/README.md). No execution result is pre-filled.

Unlock SB05 with session/section/load lifetime proven. Remaining command work is explicitly owned by SB05, not hidden as complete isolation.

## Reopen triggers

Target/session identity, cloning, section mapping, load/partial failure, reset/close behavior, child services or host parameter propagation changes.
