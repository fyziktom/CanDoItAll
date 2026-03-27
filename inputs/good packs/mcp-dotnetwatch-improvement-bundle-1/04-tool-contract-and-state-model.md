# 04. Tool Contract And State Model

## Contract goals

The public contract must let Codex ask four questions directly:

1. Which runtime lane is active?
2. Which revision is active?
3. Is a candidate update being prepared or committed?
4. Can I trust the bridge/backend path right now?

## Backward compatibility rule

Existing tool names must remain valid.

Bundle 1 should prefer additive evolution:

- old parameters keep working
- new launch modes are optional
- old callers using `mode=WatchRun|RunOnce` map into the new launch model through a compatibility shim

Breaking renames are out of scope for bundle 1.

## Public model changes

### Envelope evolution

Bundle 1 should add an optional compact guidance block to the MCP envelope rather than smuggling workflow advice into free-form diagnostics.

Suggested direction:

```csharp
public sealed record WorkflowGuidanceData(
    string Mode,
    string Next,
    string Verify,
    string? Guard = null,
    string? ReasonCode = null);
```

Suggested envelope addition:

```csharp
[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public WorkflowGuidanceData? WorkflowGuidance { get; init; }
```

Contract rules:

- guidance is optional and additive
- guidance is for selected status/control tools only
- guidance must stay compact and code-like rather than prose-heavy
- `Summary`, `Diagnostics`, and `NextSuggestedTools` remain valid and are not replaced
- raw log/event tools do not emit `WorkflowGuidance`

### `workspace_info`

Add:

- bridge status
- lane capabilities
- atomic update capability flags
- active logical apps
- slot summary for managed logical apps
- optional workflow guidance describing the preferred working mode for the current workspace state

Suggested additions:

```csharp
public sealed record BridgeStatusData(
    string Mode,
    string? BackendId,
    DateTimeOffset? LastPingUtc,
    DateTimeOffset? LastRepairAttemptUtc,
    string? CurrentShadowSignature,
    string? CurrentShadowDllPath,
    string Health);
```

### `app_start`

Keep current parameters, but add a new optional launch-spec shape.

Suggested direction:

```csharp
public sealed record AppLaunchRequest(
    string? LogicalAppId = null,
    AppLaunchType LaunchType = AppLaunchType.Project,
    RuntimeLaneKind? PreferredLane = null,
    string? ProjectPath = null,
    string? EntryPath = null,
    string? WorkingDirectory = null,
    string? ConfigurationName = null,
    string? Framework = null,
    string? LaunchProfile = null,
    string[]? Arguments = null,
    Dictionary<string, string?>? EnvironmentOverlay = null,
    string[]? Urls = null);
```

Compatibility mapping:

- `mode=WatchRun` -> `LaunchType=Project`, `PreferredLane=SourceWatch`
- `mode=RunOnce` -> `LaunchType=Project`, `PreferredLane=SourceRun`

### `app_status`

Add:

- `logicalAppId`
- `laneKind`
- `revision`
- `slot`
- `activeTransactionId`
- `workflowGuidance`

Suggested revision model:

```csharp
public sealed record RuntimeRevisionData(
    string Kind,
    string Value,
    DateTimeOffset ObservedUtc,
    bool IsConfirmed);
```

### `app_wait`

Keep existing conditions and add explicit revision/transaction waits:

- `RevisionConfirmed`
- `TransactionPrepared`
- `TransactionCommitted`
- `RollbackCommitted`

`Healthy` should remain valid, but it should no longer be the only way to model correctness.

Guidance rule:

- emit `workflowGuidance` only when the returned state is useful to act on
- do not emit repetitive coaching on every internal poll or transient wait snapshot

### `app_logs`

Keep log access intact.
Do not overload it with lifecycle semantics.
Do not add workflow guidance to this payload.

### New tool: `candoitall_app_events`

Purpose:

- incremental structured event stream without raw-log parsing

Suggested shape:

```csharp
public sealed record AppEventData(
    long Sequence,
    DateTimeOffset TimestampUtc,
    string LogicalAppId,
    string SessionId,
    string EventType,
    string Summary,
    RuntimeRevisionData? Revision,
    string? TransactionId,
    string? SlotId);
```

### New tool: `candoitall_app_update_atomic`

This is the main new Codex-facing tool for safe candidate preparation and commit.

Suggested shape:

```csharp
public sealed record AtomicUpdateRequest(
    string? LogicalAppId = null,
    string? ProjectPath = null,
    string ConfigurationName = "Release",
    string? Framework = null,
    string[]? Arguments = null,
    Dictionary<string, string?>? EnvironmentOverlay = null,
    bool ActivateOnSuccess = true,
    bool KeepPreviousRuntimeWarm = true,
    bool AllowRollback = true,
    int? TimeoutMs = null);
```

Suggested response:

```csharp
public sealed record AtomicUpdateData(
    string TransactionId,
    string LogicalAppId,
    string CandidateSessionId,
    string CandidateSlotId,
    string State,
    RuntimeRevisionData CandidateRevision,
    RuntimeRevisionData? ActiveRevision,
    string[] ObservedUrls,
    bool Committed,
    bool RollbackAvailable);
```

Response guidance examples:

- `atomic-candidate-next` while the candidate is ready for browser validation before commit
- `rollback-available` after a commit or after a failed validation that left the previous revision intact

### New tool: `candoitall_app_rollback`

Purpose:

- restore the previous committed revision for a logical app

### Optional new tool: `candoitall_bridge_status`

This can be omitted if the added `workspace_info.bridge` payload is considered sufficient.
Bundle 1 does not require both.

## Guidance emission matrix

Preferred emitters:

- `workspace_info`
- `app_status`
- `app_wait`
- `operation_status`
- `app_update_atomic`
- `app_rollback`
- `diagnose_start_failure`

Explicit non-emitters:

- `app_logs`
- `operation_logs`
- `app_events`

Emission rules:

1. the guidance block should usually contain no more than:
   - one `Mode`
   - one `Next`
   - one `Verify`
   - one short optional `Guard` or `ReasonCode`
2. if no high-confidence recommendation exists, omit the block
3. if the response is already high-volume, omit the block
4. static tool descriptions may include one short workflow sentence, but not a paragraph

## Internal state additions

### Logical app record

Suggested fields:

- logical app id
- active session id
- active revision
- previous active session id
- previous active revision
- current slot id
- last committed transaction id

### Atomic transaction record

Suggested fields:

- transaction id
- logical app id
- source signature
- target slot id
- previous active session id
- previous active revision
- candidate session id
- candidate revision
- state
- timestamps
- failure summary if any

### Slot manifest

Suggested fields:

- slot id
- logical app id
- publish hash
- entry path
- working directory
- health URLs
- created utc
- last activated utc

## Idempotency and retry policy

This is mandatory because bridge repair can otherwise duplicate work.

### Auto-retry allowed

- `workspace_info`
- `app_status`
- `app_wait`
- `app_logs`
- `operation_status`
- `operation_wait`
- `operation_logs`
- `app_events`

### Auto-retry only with request idempotency key

- `app_start`
- `solution_build`
- `tests_run`
- `app_update_atomic`
- `app_rollback`

Implementation rule:

- the stdio bridge must attach an operation/request id to non-idempotent calls
- if a reconnect happens after ambiguous delivery, the backend must deduplicate by that id

## Response quality rule

Bundle 1 must eliminate generic "invocation failed" style outcomes for known bridge/runtime categories.

Expected high-signal codes include:

- `BridgeRepairFailed`
- `BackendUnavailable`
- `BackendAuthMismatch`
- `CandidatePrepareFailed`
- `CandidateHealthFailed`
- `CommitFailed`
- `RollbackFailed`
- `ResourceConflict`
- `ValidationTimeout`

## Workflow guidance quality rule

Bundle 1 guidance must be:

- accurate with respect to the actual lane and revision state
- compact enough to avoid noticeable context pollution
- biased toward local validated iteration before broader change sets
- able to recommend escalation to build/test or atomic candidate work when local watch iteration is no longer the best fit
