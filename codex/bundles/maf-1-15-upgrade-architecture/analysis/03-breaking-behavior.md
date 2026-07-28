# Breaking and Behavior-Changing Details

## 1. Approval Response Binding

### 1.15 behavior

The default chat-client pipeline is ordered approximately as:

```text
ApprovalResponseBindingChatClient
  -> ApprovalNotRequiredFunctionBypassingChatClient
    -> FunctionInvokingChatClient
      -> optional MessageInjectingChatClient
        -> optional PerServiceCallChatHistoryPersistingChatClient
          -> telemetry
            -> provider client
```

The binding middleware:

- records each surfaced `ToolApprovalRequestContent` in the active session state bag under `_pendingApprovalRequests`;
- snapshots function-call name, ID, and arguments;
- consumes pending state on the next approval turn;
- accepts only a response whose request ID is known;
- rebinds modified response tool calls to the recorded model-originated call;
- ignores unknown and duplicate responses;
- also recognizes a trusted request present in the current message history.

### CanDoItAll consequence

The current continuation sends only messages containing `request.CreateResponse(approved)`. This works after a 1.15 run only if the exact serialized session with the binding state is restored.

A legacy 1.13 session contains no binding state. The response can be dropped before function invocation.

### Required migration rule

Never set `DisableApprovalResponseBinding = true` as a compatibility shortcut.

Preferred handling order:

1. drain, cancel, expire, or reissue legacy pending approvals before deployment;
2. require the exact serialized native 1.15 session state before accepting an
   approval continuation;
3. reject all pre-1.15, missing, or incompatible native session state with a
   typed drain/reissue outcome.

Do not inspect or mutate private MAF JSON to classify compatibility. Do not
reconstruct a legacy application record into executable 1.15 binding state.

## 2. Mixed Tool Calls Changed Default

### 1.13

The option was:

```csharp
EnableNonApprovalRequiredFunctionBypassing
```

Default: `false`.

### 1.15

The option is:

```csharp
DisableApprovalNotRequiredFunctionBypassing
```

Default: `false`, meaning bypass is enabled.

This is not a simple rename. Default behavior is inverted from the application's 1.13 baseline.

### Parity strategy

In SB02 explicitly set:

```csharp
DisableApprovalNotRequiredFunctionBypassing = true
```

on every relevant `ChatClientAgentOptions` construction.

This preserves the old all-or-nothing approval surface while approval binding and state migration are stabilized.

### Adoption strategy

After SB03 security proof, evaluate enabling the new default. Benefits:

- only true human-approval requests are exposed;
- ordinary tool calls returned in the same model response can be stored and automatically resumed;
- UI and application approval queues become cleaner.

Required application changes:

- decisions admitted only for the exact complete current server-held pending snapshot;
- exact pending-count, snapshot-change, and mixed-call tests;
- state-bag persistence across restart;
- no duplicate execution;
- no hidden auto-approval of application-classified mutations.

The MAF decorator only knows whether a tool is wrapped as `ApprovalRequiredAIFunction`. CanDoItAll's own mutation policy remains authoritative.

## 3. Handoff Terminal Output

MAF 1.15's `WorkflowHostAgent.RunCoreAsync` tracks all updates and terminal workflow output updates separately. If a terminal output exists, only that projection is used for the final non-streaming `AgentResponse`.

The current custom depth guard does not call that method. It calls its own streaming method and merges every update via MEAI.

Consequences can include:

- intermediate participant text becoming the apparent final response;
- duplicate or extra messages;
- wrong structured/typed output selection;
- different non-streaming behavior between direct MAF and CanDoItAll;
- finalizer repair being triggered by an output that existed but was projected incorrectly.

The main runtime is also streaming-first. It must implement or obtain an authoritative terminal projection rather than assuming the upstream non-streaming fix automatically applies.

## 4. Message Ordering and Merge Semantics

MAF's workflow merger now:

- retains response IDs in first-seen order;
- retains message buckets in first-seen order;
- delegates contiguous update merging to MEAI `ToAgentResponse`;
- handles id-less reasoning segments adjacent to an ID-bearing assistant message;
- preserves tool-call/result adjacency.

CanDoItAll separately snapshots and merges updates. Any custom code that:

- sorts by `CreatedAt`;
- groups only by message ID;
- assigns synthetic IDs;
- de-duplicates by text;
- moves tool results;
- drops `RawRepresentation`;
- flattens all workflow outputs indiscriminately

can reintroduce the bug outside the framework.

SB01 must find all such code. `MafAgentResponseSnapshotter` is a mandatory inspection target even if its path changes.

## 5. Session and Checkpoint State

### Chat sessions

The 1.15 `ChatClientAgentSession` JSON constructor accepts omitted `conversationId` and state-bag arguments. This improves strict serializer compatibility, but it does not replace:

- governed-step isolation;
- provider-managed conversation detection;
- attachment data scrubbing;
- transcript fallback;
- serialization timeout policy.

### Workflow external requests

Workflow session restoration now resolves request payload types against live request ports and ignores assembly-version/culture/public-key-token differences. This can help deployments that resume checkpoints after package/application version changes.

It only applies if CanDoItAll stores the native workflow checkpoint/external request envelope. The custom checkpoint bridge must be traced before deleting any compatibility code.

## 6. Harness File Access

Only `HarnessAgent` behavior changed. A `HarnessAgent` has no file-access tools unless `FileAccessStore` is supplied.

CanDoItAll's confirmed path uses custom workspace services and custom tools. No migration to Harness file access is required or desired.

A hidden Harness path would require:

- replacing removed `DisableFileAccess`;
- explicitly supplying a store for intended access;
- checking `FileAccessProviderOptions`;
- preventing duplicate tool names with CanDoItAll tools.

## 7. Warning Surface

Stable APIs lost some experimental annotations, but workflow/handoff and advanced options may remain experimental. Current project-wide `NoWarn` entries can hide both obsolete and newly meaningful warnings.

Migration procedure:

1. capture baseline warning list;
2. temporarily remove MAF warning suppressions in targeted projects;
3. classify each warning;
4. use local pragmas or narrow project suppression only where a conscious experimental dependency remains;
5. record rationale in the execution report.
