# Retained state, deployment and rollback

## Preserve state before changing the dependency graph

Capture small genuine **1.20** fixtures from a disposable host for: ordinary local/framework-managed chat, service-managed conversation where supported, pending function approval, settled tool history, an interrupted admitted batch and a paused workflow. Record package versions, envelope schema, provider/history mode and relevant authority/tool fingerprints. Redact secrets without destroying structural proof. Never use fabricated internal pending-approval state as a stand-in for what 1.20 actually serialized.

Existing native state already flows through `MafRuntimeStateAdapter`, `MafRuntimeStateCompatibilityPolicy`, `MafRuntimeSessionBuilder` and `MafToolRunContext` [R10–R13]. Improve these owned seams; do not introduce parallel ad hoc storage or private upstream state-field manipulation.

## Explicit compatibility outcomes

For each fixture under 1.22 choose and test one outcome: supported native restore; a small registered migration that preserves meaning; canonical transcript replay for a case without unsettled protected effects; or fail-closed reconciliation/re-approval with an actionable safe explanation. These are semantic outcomes; preserve the current typed API names where practical.

Native approval authority, application approval receipts and durable effect/proposal state must agree. A missing native checkpoint is not an invitation to reconstruct permission from display history. Conversely, do not wipe all chats merely because one guarded legacy case cannot resume. Keep historical content and artifacts readable without allowing unauthorized execution.

The current same-major package test [R12] automatically admits 1.20/1.22 at that layer, and permits unknown version strings. Explicitly review that behavior for pending governed work. Do not broaden compatibility by mutating stored version labels or bypassing provider/policy/schema checks.

If renamed A2A modes or session-store key formats are actually persisted, add only the necessary tested mapping/reader behavior. Preserve key boundaries and old-record ownership; delimiter collisions or missing tenant/project partitions must not merge records. Do not assume a CLR namespace change always requires a database schema migration: inspect the actual serialization format first.

## Deployment note to maintain

Document the actual dependency bump, supported retained-state cases, any operator action for paused/pending runs, how to identify incompatible records without exposing contents, and the relevant evidence/backup procedure. No automatic destruction of databases or journals. PostgreSQL 18 is already the baseline; do not combine a second database upgrade with this change.

For a real deployment, drain or deliberately pause dispatch, preserve consistent backups of application databases plus workspace/native journals/checkpoints/artifacts, and verify the restored checkpoint in a disposable environment before resume. Define the rollback window and which version last wrote each state family. A binary downgrade after 1.22 writes new state is not automatically safe.

## Rollback acceptance

A safe rollback restores mutually consistent application data, journals/checkpoints and artifacts from the same verified boundary, or uses an explicitly proven backward reader. Never restore only a database while keeping newer mutation receipts or workspace effects. External side effects cannot be undone by rolling back local files; preserve reconciliation and business-operation identities.

Test rollback/recovery procedures only on disposable fixtures in this task. Do not execute a customer/retained-environment rollback, credential rotation, package publication or deployment without explicit authorization. Record limitations rather than claim universal forward/backward compatibility.
