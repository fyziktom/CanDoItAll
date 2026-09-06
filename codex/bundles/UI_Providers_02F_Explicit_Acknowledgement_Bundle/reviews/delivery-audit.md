# Delivery producer and receiver audit

| Owner | Production path | Acknowledgement / failure behavior |
|---|---|---|
| SharedProviderChangeDelivery | Receiver envelope | Only completed ReconcileAsync marks acknowledgement. One active task per envelope; failures/cancellation permit explicit retry. |
| SharedProviderRecovery | Target/source delivery coordinator | Requires explicit envelope acknowledgement and matching active attempt/current owner before cleanup. Callback success alone remains Pending. Already acknowledged envelope skips callback. |
| SharedProviderManagementPanel | Publish, Unpublish, imported settings, retirement and canonical verification | Records known change in scoped recovery before owner checks; DeliverTargetAsync forwards the envelope. Its Retry never repeats a resolved write. |
| SharedProviderSourcesDialog | Source create/update/enable/delete/test/sync, canonical verification and delivery retry | Known changes retain their attempt and call DeliverSourceAsync. The only attempt-less PublishChangeAsync call is the explicit Unconfirmed/UnknownScope advisory; it is not a retained commit. |
| SharedProviderRefreshButton | Synchronize selected imports, then delivery retry | Records commit before lifetime checks and uses DeliverSourceAsync. Unconfirmed failure emits an unretained advisory. No callback default can now unlock a retained commit. |
| ProviderModelThinkingEditor | Forwards Refreshed to the enclosing provider workspace | Preserves the envelope; does not substitute a Changed event or acknowledge itself. Missing parent leaves committed delivery pending. |
| AgentProviderProfilesPanel | Sharing, source overlay and thinking-refresh receiver | ReconcileAsync wraps session.ReconcileSharedAsync, raw-field sync only when replacement occurred, sharing revision and tree metadata. Throws when Completed is false. Draft and EditContext preservation stay in the existing session. |
| AgentDetailsDialog | Source-managed runtime provider refresh receiver | ReconcileAsync wraps RefreshRuntimeProvidersAsync; it checks session ownership before/after the provider read and throws on stale/canceled or failed reads. No swallowed refresh failure can acknowledge. |

Repository search for EventCallback<SharedProviderChangeDelivery>, Refreshed/ProvidersChanged bindings, RecordCommit and DeliverTargetAsync/DeliverSourceAsync found no other production receiver. Test callbacks that intentionally represent successful parent work now explicitly invoke ReconcileAsync; no-op callbacks remain only negative tests or callbacks that must never be invoked. No production receiver required alteration.

A receiver can finish while its sender disappears: acknowledgement remains in the circuit envelope, but sender bookkeeping still requires a valid current target/source. Circuit teardown remains the durability boundary. This does not promise distributed exactly-once processing or persist any delivery receipt.
