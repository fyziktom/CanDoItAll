# WCL-DEL1: Agent deletion cannot confirm cleanup after completed history runs

Status: **OPEN, P2**. The failure crosses the execution, chat, usage and catalog deletion
owners. Its root cause is unproven. The actual history acceptance attempt remains FAILED;
successful history assertions alone are not closure.

## Original operation, scope and effects

The private client runs published application `3d7c88f384b7920744464a7c7570530ca825325a`
with Components `22d5b21afdf80c2bca74c1c598f0b1bb72c86f9e`. The test observation correction,
subsequently signed in `2f658cda606c8ab13f71025ff33023b9c220bcad`, only compares the exact
legitimate request identities. It does not change the deletion or provider protocol.

`final-shared-history-02` executes two Agent turns against the scripted external fixture.
Both complete and create durable session/run/history records. Exact global/provider history
IDs agree: two client attempts, four publisher attempts for credential A (tool and result
continuations), and one disjoint direct-relay attempt for credential B. Lazy-load and content
assertions pass. Cleanup confirms deletion through the actual Agent editor, but it reports
"Agent delete failed" and "The deletion result could not be confirmed. Reload the catalog
before retrying." A manual UI attempt reproduces the failure at 18:11:25.611Z; a fresh page
reload still shows the same Agent. No successful deletion, rollback or partial durable
effect is inferred. Further deletion attempts stopped.

The original Agent ID is `6fba9f8d-329c-43b5-ae00-afe220d5a56a`; session ID is
`c27a59ce-34ca-4e7d-84e7-68a632b06ae0`. The runs are
`723aea90-860f-435f-b8b2-a5f70d0cf4c2` (completed 18:04:47.4395887Z) and
`af767546-37e5-4c28-8287-e93b3d1b8f61` (completed 18:04:57.9636134Z).
Their original scope in owned container `candoitall-wcl-client-6345bb57` is
`/data/workspace/data/scopes/organization/356015a2e7dfe2cea5be277c35b6c071`.
These are task fixture identities, not retained customer data.

## Causal map and limits

The call chain is `AgentDetailsDialog.DeleteAgentAsync` → `AgentEditorCommands` →
`CurrentProfileAgentFrameworkWorkspaceService` → `AgentFrameworkWorkspaceCatalogService`
→ `FileSandboxWorkspaceStore.DeleteAgentWorkspaceDataAsync` → coordinated execution/chat/
usage/catalog deletion and journal → CRM/HR projection refresh. The current UI catch does
not retain the underlying owner exception, so the failing stage is unknown. The server log
does not establish a PostgreSQL delete/foreign-key error. A confirmation dialog does not
make this a Dialog lifetime defect.

Original read-back shows both runs Completed/Succeeded at revision 8, zero pending
approvals and `HasUnresolvedEffects=false`; summary identities match. Execution, chat and
usage indexes all have revision 177 and agree on five sessions/five runs. No pending Agent
deletion journal exists. Both stored run records retain non-null active dispatch lease IDs,
but the inspected deletion guard uses state/unresolved effects; that field is not an
established cause. The evidence cannot prove which irreversible commit stage, if any, ran.

The full test and manual reproduction are current-source observations. No claim is made
that this regression originated in the closure edits. Do not force-delete the fixture,
widen grants, rewrite indexes, replay services, remove the cleanup assertion, or recreate
the workspace and present that as preservation of the original effects.

## Evidence and bounded continuation

Ignored root: `artifacts/workspace-closure/20260930-d9273a889`.
`history-delete-finding-final.json` contains the safe projection and original byte hashes;
`final-shared-history-02-media/history-attempt-identities.json` contains only compared IDs.
`history-agent-delete-failure.png` shows the retained editor after the toast expired; it is
not an image of the toast. The prior projection's `originalSha256` fields were hashes of
PowerShell-normalized text; only the final finding's byte hashes identify original files.

`agent-deletion-private-snapshot.dpapi` preserves the original directory tar protected with
Windows DPAPI CurrentUser and null entropy. `agent-deletion-snapshot-receipt.json` records
plaintext/protected hashes and restore instructions. The plaintext temporary tar was
removed. The encrypted original remains private after owned fixture cleanup, and can be
restored only into a new private investigation directory under the same Windows account.

`agent-deletion-private-database.dpapi` also preserves the original
`wcl_shared_client_6345bb57` database as an encrypted PostgreSQL custom-format dump.
`agent-deletion-database-receipt.json` records its source container, capture time, tool
version and plaintext/protected hashes. The shared client was already stopped, and its
database was captured before removing the owned PostgreSQL instance. This preserves the
database-backed projection state alongside the original file indexes. The plaintext dump
was removed. These are state artifacts, not a replayable runtime backup: transient signing
and data-protection keys/environment files were deliberately cleaned. Restore only into a
new isolated database/directory; never reconnect copied provider configuration to a service.

First capture the typed exception at the owning service boundary against a private clone
of this preserved state. Then hold deletion admission and each commit stage independently,
with completed-run and active/unresolved-effect negative controls. Only after causal
attribution select a bounded correction in the existing transaction or projection owner.
Any durable multi-file/authority change needs its owning regressions and a new affected
broad checkpoint. This mapping is an open finding, not a repair or a permission to change
the transaction contract. Independent safe validation continues.

See the [closure report](workspace-critical-fixes-closure.md) for the failed acceptance
row, source binding, cleanup receipt and separate readiness blockers.
