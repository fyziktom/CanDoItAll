# Normalized Requirements

| ID | Requirement | Observable acceptance |
| --- | --- | --- |
| R01 | Return an operation handle and emit typed activity before execution-run creation. | Synchronous start returns stream ID/completion before the command finishes; a deterministic test blocks catalog loading and replays `Accepted`/`CapturingContext` from sequence zero with no run ID. |
| R02 | Correlate pre-run activity with the created run without magic strings. | New execution requests require the typed initial operation ID, the stream emits run binding, and the legacy-nullable persisted run field matches it. |
| R03 | Keep ephemeral activity separate from durable execution history. | UI-critical publication succeeds without a run-detail save; durable log failure is explicit and does not silently relabel the activity as persisted. |
| R04 | Provide isolated, typed pub/sub suitable for future SSE projection. | Scoped authorized readers cannot receive another profile/workspace operation, have primitive-owned sequence, bounded terminal/tombstone replay, explicit gap/evicted/unknown and capacity results, and dispose independently of command cancellation. |
| R05 | Reuse immutable current module context. | Contributor-owned typed project/process attachments reach runtime-tool construction from already-loaded projections through the existing transient-context lease; a covered snapshot read performs zero storage calls and attachment fingerprints are bound into approval-continuation integrity without object bags or a second store. |
| R06 | Define snapshot source-of-truth/freshness policy. | Tests independently prove monotonic publication order, content/selection fingerprint, coverage fingerprint, profile generation, typed expiry, exact source/scope/contributor/kind/type eligibility, explicit coverage miss versus canonical-current dispatch, field-complete workspace/live process revision vectors, and that snapshots are structurally unavailable to canonical write paths. |
| R07 | Prepare reusable immutable runtime descriptors. | Warm acquisition reuses a blueprint matching catalog data revision, profile generation, and provider fingerprint; live runtime resources and per-invocation context remain excluded. |
| R08 | Use safe parallel initialization where independent. | Parallel stages use separate thread-safe/factory-created dependencies; tests prove no shared-DbContext concurrency and cancellation/failure behavior. |
| R09 | Measure actual backend improvement before UI work. | Reproducible baseline/after artifacts show immediate first activity and a documented material improvement or a recorded no-go that blocks SB06. |
| R10 | Provide truthful Blazor feedback on both chat surfaces. | Floating and process-manager chats render current typed phase from submit through completion/failure/approval without relying on selected run state. |
| R11 | Preserve existing behavior and architecture boundaries. | Targeted unit/component/integration tests, dependency snapshot, and architecture gate pass with no product dependency reversal. |
| R12 | Control validation cost. | Any real provider-backed agent test uses `gpt-5.4-mini`, not Terra; non-provider tests remain deterministic. |
| R13 | Update product and SharedInfo documentation. | Relevant architecture/API/skill docs describe ownership, contracts, lifecycle, authorization boundary, and examples; repository validators pass. |
| R14 | Deliver a testable running host. | Solution rebuild succeeds, the intended host is restarted on port 5032, and an HTTP/browser health check succeeds. |
