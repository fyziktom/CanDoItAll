# 18 · Observability, rollout and operational recovery

## Trace what the user actually experienced

Correlate UI submission, tool invocation/proposal, owner operation and accepted effect without merging their identities. Structured tracing should indicate owner/operation kind, outcome category, source/target validity class, transaction phase, current attempt and whether the owner knows commit succeeded. Do not store raw confidential drafts, prompt text, access tokens or connection strings in routine logs.

Metrics use bounded labels such as owner, command kind, outcome and dependency category. Individual project/user/operation IDs belong only in appropriately controlled trace correlation, not unbounded metric labels. A token/hash is not automatically safe to disclose if it links sensitive activity.

## Required measures

| Signal | Purpose | Interpretation |
|---|---|---|
| Target conflicts / accepted commands | Finds stale editors and too-broad revision scopes | Higher rate can reflect more detected existing races, not a regression alone |
| Basis invalidation by fact class and age | Shows obsolete planning and false dependencies | Compare against useful completed work and replan cost |
| Technical retry and semantic replan counts | Separates database contention from changed intentions | They need distinct limits and alerts |
| Unconfirmed outcomes and age | Detects lost-response recovery problems | Never treat as automatic retry backlog |
| Local gate / DB lock / connection waits | Identifies actual contention source | Report wait separately from held critical-section time |
| Transaction/SQL count and row volume | Finds N+1 or full-graph validators | Compare same workload and exact source version |
| Receipt size/retention pressure | Prevents silent loss of dedupe evidence | Full-detail expiry differs from replay-safety expiry |
| Projection lag/backlog/dead letters | Detects missing/blocked derived state | Required work cannot be silently discarded |
| Rejected stale worker generation | Makes takeover/fencing visible | A late attempt rejection can be expected safe behavior |
| Draft reconciliation failures | Finds client outcome/UI defects | Do not fix by reissuing the mutation |

## Operator recovery playbooks

**Content conflict:** preserve draft/proposal, obtain safe current state, resolve intention and issue a new operation with current observed conditions. Do not copy a new token into the original body invisibly.

**Unconfirmed save:** retain the original operation ID; query the authoritative owner status and verify scope. Pending waits are bounded. A committed result is reconciled; an exact known rejection can be corrected. Missing evidence needs the protocol's explicit recovery, not a new create ID.

**Projection backlog:** diagnose the durable intention/version and source/lifetime. Retry only the permitted projection/rebuild. Do not replay the original project save. Repair tools must use the same ordering/deletion rule.

**Stale or revoked worker:** keep the late result as nonapplied evidence. Confirm current attempt/mandate, then explicitly re-admit or discard. A cancellation request does not justify treating a delivered external effect as absent.

**External uncertain effect:** query provider-specific receipt/identity or invoke the documented manual reconciliation. Do not automatically resend where the provider cannot deduplicate.

**Restore/mixed version:** pause affected writes, reconcile dataset epoch and old client/worker state, then activate only compatible writer paths. Do not drop receipts or disable preconditions to force startup.

## Rollout order

Introduce diagnostics and focused fault tests, then a coherent owner enforcement cutover. Use internal/test environments before wider activation. Each release notes exactly which owners/operations are protected, required caller versions, supported old-record behavior and known limitations. A partial feature rollout cannot leave an alternate unsafe path to the same protected aggregate.

Performance alarms should trigger investigation, bounded backpressure or pausing the affected feature—not last-write-wins fallback. An optional UI notification system can fail without invalidating owner correctness. A required durable outbox must have capacity/lag response procedures.

## Operational gates

Before advertising the new guarantee, rehearse database restart, application restart, dropped response, retention expiry, project deletion/recreation, import and destructive restore. Confirm no unconverted automation remains. Record the rollback-to-compatible-build procedure and point beyond which forward repair is required.

Revisit the guarantee after changes to owner schema, source authority, transaction primitives, tool journals, renderer lifetimes, dataset activation, storage drivers and relevant third-party versions. This is a change-triggered review policy; this package does not schedule autonomous monitoring or perform future work.
