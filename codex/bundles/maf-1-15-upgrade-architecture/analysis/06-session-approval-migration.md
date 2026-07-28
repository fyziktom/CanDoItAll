# Session and Approval State Migration

## State Layers

CanDoItAll currently has at least four relevant state layers:

1. sandbox/application chat transcript;
2. custom compatibility record containing serialized MAF session JSON and pending approval summaries;
3. MAF `AgentSession` / `AgentSessionStateBag`;
4. provider-managed conversation or response ID.

Workflow runs may add a fifth layer: native MAF workflow checkpoint/external request state.

The migration must version and classify these layers instead of treating all JSON as equivalent.

## Proposed Compatibility Metadata

Add metadata outside the opaque MAF JSON. Suggested fields:

```text
FrameworkFamily = "Microsoft.AgentFramework"
FrameworkPackageVersion = "1.13.0" | "1.15.0"
RuntimeStateSchemaVersion = integer
ApprovalStateSchemaVersion = integer
CreatedAtUtc
LastPersistedAtUtc
ProviderKind
ProviderTransport
HistoryMode
HasPendingApprovals
PendingApprovalCount
ApprovalSetFingerprint
ContainsProviderConversationId
ContainsRequestScopedAttachments = false after scrub
```

Do not rewrite internal MAF state-bag JSON keys as a normal migration strategy.

## Approval Fingerprint

For each persisted request, compute an application-owned fingerprint over canonical fields such as:

```text
session-id
execution-run-id
process-run-id
process-step-id
approval-id
call-id
tool-kind
tool-name
server/endpoint detail
canonical arguments JSON
workspace scope kind/key
allowed external target aliases
creation timestamp or nonce
```

Use a deterministic canonical JSON representation and SHA-256. Store the fingerprint with the pending record. If the state store is attacker-modifiable, use an HMAC or authenticated storage rather than a plain digest.

## State Classifier

At continuation time classify:

### C1 — Native 1.15 pending approval

Conditions:

- state metadata says 1.15+;
- exact MAF session restores;
- binding state was serialized;
- custom pending record and MAF request IDs agree.

Action: continue normally with a per-ID decision.

### C2 — Legacy 1.13 pending approval

Conditions:

- source version 1.13 or missing new state metadata;
- custom pending record exists;
- no trustworthy native 1.15 binding state.

Preferred action: invalidate and reissue the approval under 1.15.

Optional temporary bridge: replay a server-trusted reconstructed request plus the decision, after fingerprint validation and one-time consumption.

### C3 — Pending record without serialized MAF session

Action: do not execute. Reissue or fail with a recoverable typed status. Current behavior already rejects some of these cases and should remain fail-closed.

### C4 — No pending approvals, framework-managed history

Attempt 1.15 deserialize. If incompatible, use the existing transcript fallback only where semantics allow it. Governed process steps should remain isolated.

### C5 — Provider-managed conversation

Preserve the provider conversation/response ID rules. Do not replay the entire transcript into a provider-managed conversation and duplicate history.

### C6 — Workflow checkpoint/external request

Use a dedicated native workflow fixture. Do not assume chat-session behavior applies.

## Changes to Approval API

Replace:

```text
RespondToPendingApprovals(sessionId, approved: bool)
```

with a request-specific contract, conceptually:

```text
RespondToPendingApprovals(
    sessionId,
    decisions: [
        { approvalId, approved, reason?, expectedFingerprint? }
    ])
```

Rules:

- every decision must match one current pending approval;
- missing and duplicate IDs fail;
- unknown IDs fail;
- omitted pending IDs remain pending or are explicitly rejected according to product policy;
- a decision is consumed once;
- the response uses the original server-held request/tool call;
- UI must display exactly the arguments being approved;
- a batch cannot silently apply one decision to newly arrived approvals.

## Remove Random Request IDs

Current mapping falls back to `Guid.NewGuid()` when both request and call IDs are missing. Under exact binding this is not safe.

New rule:

- a surfaced approval without a stable request ID is a framework/provider contract failure;
- log a redacted diagnostic;
- do not present it as approvable;
- do not execute it;
- capture a fixture and escalate as a recoverable runtime failure.

## Serialization and Scrubbing

After a 1.15 run with pending approvals:

1. serialize the session;
2. apply the request-scoped attachment scrubber;
3. deserialize the scrubbed result;
4. assert the pending approval binding state still exists and is usable;
5. assert no attachment bytes remain;
6. restart the process;
7. approve one exact request;
8. prove exact-once invocation.

Also test MCP tool-call shapes, because the upstream binding snapshots function calls specially while other tool calls may retain different runtime shapes.

## Legacy Deployment Options

### Option A — Drain and reissue (preferred)

- stop creating new approvals shortly before deployment;
- allow operators to resolve or cancel existing approvals;
- mark unresolved 1.13 approvals as requiring reissue;
- deploy 1.15;
- re-run the affected step to surface a native 1.15 request.

Advantages: simplest and strongest security.

### Option B — Controlled compatibility bridge

Use only if long-running approvals cannot be reissued.

Requirements:

- feature flag defaults off;
- only records created by the server are accepted;
- HMAC/fingerprint and original run ownership validated;
- request and response are replayed together;
- one-time nonce consumed transactionally;
- no random ID reconstruction;
- no user-supplied tool call or arguments;
- telemetry and expiry;
- removal date and migration count tracked.

### Option C — Disable binding

Rejected.

## Rollback Consideration

A 1.15 session can contain new state-bag entries unknown to 1.13. Even if the generic state bag happens to deserialize, rollback compatibility is not guaranteed.

Before rollout:

- backup the state store;
- capture 1.15-to-1.13 rollback fixtures;
- prefer restoring the pre-deployment snapshot for active approvals;
- never continue a 1.15 approval under 1.13 without an explicit security review.
