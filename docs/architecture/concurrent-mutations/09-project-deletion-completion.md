# Project deletion completion and cleanup retry

Status: stage 2 source is authored and under review. PostgreSQL execution, generated
OpenAPI/client compatibility and final static validation remain open. This contract
does not claim durable P03 deletion receipts or completed SPA cutover. The source
checkpoint is [recorded in the execution bundle](../../../codex/bundles/concurrent-mutations/validation/P05-R1-deletion-handoff-source.md).

`DELETE /api/projects/{projectId}` returns 200 only when the owner acknowledged the
SQL deletion and participant completion finished. A 409 with
`projects.delete-cleanup-pending` also confirms that the SQL deletion committed. Its
declared response model is `ProjectDeletionCleanupPendingApiResponse`, containing the
public `ProjectDeletionRecovery`. A lost commit acknowledgment is a separate uncertain
outcome; a later disposal exception cannot establish either rollback or acknowledged
deletion.

The recovery's additive `ownerCompletionUnconfirmed` field defaults to false:

| Value | Meaning and permitted follow-up |
| --- | --- |
| `false` | Owner release was confirmed. Real unfinished participant operations may be retried using their exact project, participant and recovery IDs and current authorization. An active participant claim may still require waiting; a retry refreshes that participant's claim state. |
| `true` | SQL deletion was acknowledged, but owner release or completion could not be confirmed. Retain the exact real IDs and committed-deletion result for operator review. Do not dispatch an independent cleanup retry until release is independently established. |

`GET /api/projects/deletion-cleanups` observes participant state. A refreshed or empty
list does not prove that a previous source/catalog lease was released and cannot clear
the second state. The native caller keeps that uncertainty for the exact target and
checks it both before dispatch and when queued callbacks run. There is no fabricated
participant ID when the owner had no prepared participant obligation. A confirmed
deletion must not be repeated to finish an earlier cleanup.

The native sticky hold belongs to the current host component state. It is not durable
server-side release evidence across fresh clients or restarts and does not globally
fence every caller. Durable P03 owner reconciliation and cross-client recovery remain
open; neither the pending-list endpoint nor the retry endpoint supplies that protocol.

When owner release is established and retry is permitted, use
`POST /api/projects/{projectId}/deletion-cleanups/{participantId}/{recoveryId}/retry`
with the original real identifiers. The backend still performs the participant's
current admission; a recovery identifier is not authority. This contract introduces
no automatic recovery or alternate operation identity.

Workbench creation compensation retains the same distinction. If its compensating
SQL deletion committed but cleanup did not complete, the existing partial-operation
result carries `CompensationDeletion`, keeps creation observed and the requested
transfer incomplete, and disables automatic retry. It must not say that compensation
failed to delete the project or that the requested transfer succeeded.

The HTTP model and runtime response carry this field directly. Generated OpenAPI and
client outputs must be reviewed after integration; runtime JSON alone is not proof of
those artifacts. The actual HTTP source test uses authorization-disabled fixture
requests to isolate disposal/serialization behavior, not to prove authentication.
