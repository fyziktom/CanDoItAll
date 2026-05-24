# Canonicality risks

## C1: Runtime profile vs persisted activation

After activation, the persisted active profile may differ from the database currently used by the running process. This is intended under restart-first activation, but it must be explicit in models and UI.

Required model:
- `RuntimeProfileId`
- `RuntimeProfileDescriptor`
- `RuntimeFingerprint`
- `PendingActivationProfileId`
- `PendingActivationDescriptor`
- `RequiresRestart`
- `ActivationGeneration` or timestamp

The UI must never call the pending activation "current runtime".

## C2: Stale process dispatch workers

A worker that loses or fails to renew `AutomationDispatchClaimToken` must not be allowed to commit artifacts, status transitions, branch outcomes, or failure states. Claim-token ownership must be a production invariant.

## C3: Parallel claimed work

Parallel processing is safe only if every item remains guarded by a lease token and if shared aggregate rows are updated atomically or partitioned. Envelope aggregate state and process run state are the biggest risks.

## C4: Maintenance DB access

Opening non-canonical profile-specific contexts is valid only for maintenance:
- schema check,
- create/bootstrap,
- settings transfer,
- migration proof.

It must not be used by normal runtime modules, background agents, process/workflow execution, or cognitive memory runtime.
