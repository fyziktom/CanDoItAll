# Production artifact matrix

| Artifact | Producer | Consumer | Lifetime/negative proof |
|---|---|---|---|
| Canonical provider receipt | DatabaseProviderProfileRegistry SaveCore/Delete/secondary notification | ProviderEditorCommands/Operations; ProviderApiResults | Real PostgreSQL post-commit observer/projection failures and zero-replay reconciliation, pending/new/selection/disposal tests. |
| Immutable provider submission | ProviderEditorSubmission.Capture before await | Registry request and session later-edit comparison | Public completeness/independence guard and delayed UI Save. |
| Scoped shared change | SourceService/Sync/Reconciliation/Publication/Management after authoritative persistence | Shared panels -> ProviderProfilesSession.ReconcileSharedAsync | Immutable ID sets, affected/unaffected imports, malformed/retired target, late A and overlay disposal tests. |
| Permanent publication identity | Explicit SetPublication/Publish transaction | Sharing read, public catalog, deletion guard | Side-effect-free read regression, Publish/Unpublish/delete and observer-cancellation database/API proofs. |
| Non-persisted health failure | Runtime administration diagnostic catch before registry Update | Editor health outcome and sanitized API 502 | Actual throwing diagnostic, unchanged persisted revision, retryable command result. |

Exact methods, results and source hashes are referenced by the SBC manifest. Static call shapes support, but do not replace, production-adapter and rendering behavior evidence.
