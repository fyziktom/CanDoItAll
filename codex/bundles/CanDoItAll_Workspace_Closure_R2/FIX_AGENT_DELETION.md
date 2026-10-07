# P1 — WCL-DEL1: actual Agent history deletion

## Evidence, not an assumed diagnosis

The maintained finding reports an Agent that completed two scripted-provider turns and whose
actual editor Delete then failed; a manual UI retry and fresh reload retained the Agent.
The source owner exception is not retained in that public finding. It is not established
that a foreign key, an active dispatch lease, a driver, or the dialog caused the failure.
P2 describes the recorded priority; it does not mean the fix is necessarily small. [R03]

Original fixture identities (diagnostic context only, never production commands):

- Agent `6fba9f8d-329c-43b5-ae00-afe220d5a56a`;
- chat `c27a59ce-34ca-4e7d-84e7-68a632b06ae0`;
- runs `723aea90-860f-435f-b8b2-a5f70d0cf4c2` and
  `af767546-37e5-4c28-8287-e93b3d1b8f61`;
- reported indexes at revision 177; both runs terminal, no pending approvals/unresolved effects.

Use the original private finding and encrypted snapshot/database receipts from
`artifacts/workspace-closure/20260930-d9273a889` when available. Verify the hashes recorded
for original bytes, not text normalized by a scripting shell. DPAPI data requires the same
account; lack of that key is a blocker, not an invitation to bypass protection. Transient
runtime keys were cleaned, so reconstruct only a safe test environment, not an external
network-connected clone. Keep originals immutable; all trials use disposable copies.

## Actual owner map

1. `AgentDetailsDialog.DeleteAgentAsync` receives the explicit confirmed target/session.
2. `AgentEditorCommands` and current-profile workspace adapter call the catalog owner.
3. `FileSandboxWorkspaceStore.DeleteAgentWorkspaceDataAsync` holds its gate and cross-process
   coordination, normalizes/reads source catalog, loads execution/chat indexes and prepares
   exact target deletion state.
4. `FileSandboxWorkspaceExecutionSliceStore.PrepareAgentDeletionAsync` rejects active or
   unresolved runs, validates detail/index identity, loads matching orphan records, derives
   target counts and usage, and constructs a deletion plan.
5. The outer store validates and writes `pending-agent-deletion.json` before committing
   execution/usage/chat, catalog, and workspace index. The journal is removed last.
6. The normal application may then refresh the CRM technical-agent projection and UI catalog.

The exact runtime call graph must be confirmed on the checkout. [R10, R11]

## Investigation protocol

Capture a safe operation correlation ID, profile/generation, target Agent, acquired revision,
entered stage, exception type and an allowlisted classification. Keep raw secret-bearing
payloads or exception messages out of general logs and UI. Catching an exception just to
show the existing safe message without a private correlated diagnostic is insufficient.
Log receipt confirmation distinctly from postcommit projection/refresh failure.

Create failing-first controls around the actual boundary, using existing injection seams or
a narrowly scoped test interceptor where required. Prefer reusing the real journal machinery,
not injecting a fabricated successful deletion. Cross-process-sensitive state needs the real
coordination path, not only a semaphore inside a fake service.

Important: `LoadIndexForAgentDeletionAsync` may persist a session-count correction before
journal creation. Record before/after bytes at every reached stage. No journal is not a
proof of no writes. Similarly, unchanged Agent catalog is not proof that every other index
remained unchanged. [R11]

First distinguish a deterministic precommit refusal, an exception before journal admission,
a journaled partially completed deletion, and deletion completed before a secondary failure.
Use exact owner read-back and the existing recovery protocol, not catalog visibility alone.

## Allowed correction

Implement the smallest owner-correct fix after diagnosis. A normal inactive unprotected
Agent with settled runs must be removable through its existing supported UI path, with all
its applicable records handled according to the current domain contracts. Keep the original
transaction/journal format and stable target identity unless a separately reviewed compatible
change is proved necessary. A broad schema or history-retention redesign is not authorized.

Preserve strict corruption/ownership checks. Do not remove validation because a fixture hits
it, blindly rebuild every index, treat all exceptions as a known rejection, force-remove files,
or globally retry deletion. If legitimate old/new representations are involved, identify the
specific compatible conversion and prove preservation of all affected records.

If the durable deletion already committed and only projection/refresh failed, retain that
fact and ID and provide the proper observation/reconciliation path without deleting again.
Do not invent cross-domain cascade policies for provider history or projects.

## Acceptance matrix

| ID | Controlling proof |
| --- | --- |
| DEL-01 | Exact retained-state clone or faithful new two-turn reproduction fails before the correction with captured safe stage/type. Original-state evidence and new-fixture evidence remain distinct. |
| DEL-02 | Same real editor action after correction removes the intended Agent; independent catalog, execution, chat, usage and existing projection owners reconcile according to their contracts. |
| DEL-03 | A second unrelated Agent with real history, sessions, usages, capabilities and links is byte/semantically preserved; global counters equal actual surviving owner records. |
| DEL-04 | Active execution, pending approval, unresolved durable effect and managed seed controls are refused with no unauthorized destructive effect. Completed runs with a retained dispatch ID are classified by actual policy, not guessed cleanup. |
| DEL-05 | Inject interruption before journal, after each existing commit boundary and before journal removal. Restart/observation uses the same original journal, reaches a consistent allowed outcome and does not touch another profile. |
| DEL-06 | Cancellation before admission refuses; loss of UI after durable admission does not cancel or replay the accepted commit. A late reply cannot close/reset a successor editor. |
| DEL-07 | Corrupt, revision-conflicting and ownership-conflicting sources remain safely refused; repeated explicit post-completion observation is harmless without recreating deleted history. |
| DEL-08 | Real shared-provider history acceptance cleanup succeeds after two terminal scripted turns; exact provider history identity assertions remain. No replacement empty fixture or omitted cleanup. |
| DEL-09 | Repeat source/published host and applicable Windows/Linux actual filesystem cases. Inspect complete shared logs and any related CRM projection warnings. |

Rerun `AgentDetailsDialogDeletionTests`, the actual deletion persistence and recovery suites
found by current source discovery, `ProviderHistoryUiAcceptanceTests`, and relevant catalog,
projection and usage consumers. This list gives search roots, not claimed current test counts.

## Stop condition

The existing DEL1 task is repair work, not map-only work. Continue through diagnosis and a
bounded correction. If the demonstrated change exceeds the allowed domain contracts, retain
a minimal failing test, exact state transition, blast-radius map and compatible design as an
explicit blocker. Never convert unknown cause into a fabricated fix to move to Projects.
