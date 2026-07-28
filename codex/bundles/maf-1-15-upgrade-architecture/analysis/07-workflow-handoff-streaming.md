# Workflow, Handoff, Streaming, and Terminal Output

## Current Call Paths

### Direct non-streaming workflow path

```text
WorkflowHostAgent.RunAsync
  -> WorkflowHostAgent.RunCoreAsync
    -> collect workflow updates
    -> mark terminal workflow output updates
    -> merge terminal outputs when present
    -> persist merged messages
```

This is where MAF 1.15's terminal-output fix applies.

### Current custom non-streaming wrapper path

```text
HandoffDepthGuardAgent.RunAsync
  -> HandoffDepthGuardAgent.RunCoreAsync
    -> HandoffDepthGuardAgent.RunCoreStreamingAsync
      -> InnerAgent.RunStreamingAsync
    -> collect every update
    -> MEAI ToAgentResponse
```

This bypasses `WorkflowHostAgent.RunCoreAsync`.

### Full CanDoItAll path

```text
MafAgentRuntime
  -> provider streaming runner
    -> runtime agent / handoff depth guard streaming
  -> snapshot every update
  -> collect all updates
  -> updates.ToAgentResponse()
  -> extract approvals / text / usage
```

This is the production path that must be made authoritative.

## Why a Simple Wrapper Change Is Insufficient

Changing only `HandoffDepthGuardAgent.RunCoreAsync` to call `InnerAgent.RunAsync` would fix direct non-streaming callers, but the main runtime still uses streaming.

The streaming MAF path computes and persists a merged final response internally after yielding updates, but it does not necessarily emit that merged response as another update. Therefore CanDoItAll cannot assume `updates.ToAgentResponse()` equals the framework's terminal projection.

## Required Characterization Fixture

Use a deterministic fake participant workflow with:

- entry agent emits text A;
- handoff tool call;
- second agent emits reasoning and text B;
- tool call C and result C;
- second handoff or return-to-previous;
- explicit terminal workflow output T;
- intermediate `AgentResponseEvent` and `AgentResponseUpdateEvent`;
- distinct message IDs and one id-less reasoning segment;
- known usage and author names.

Capture:

1. direct inner `RunAsync`;
2. direct inner `RunStreamingAsync` raw updates;
3. current depth-guard `RunAsync`;
4. current depth-guard `RunStreamingAsync`;
5. full `MafAgentRuntime`;
6. serialized workflow session/history.

Compare:

- selected final text;
- number and order of messages;
- tool call/result adjacency;
- author names;
- response/message IDs;
- reasoning/text ordering;
- usage;
- raw event types;
- terminal output visibility;
- history written after completion.

## Candidate Designs

Codex must choose the smallest design that passes the fixture.

### Design A — Separate activity stream from authoritative result

Run the workflow through an API that preserves MAF's authoritative non-streaming final response while subscribing to workflow events through the execution environment/activity sink.

Use when:

- token-level workflow streaming is not essential;
- activity events are sufficient for UI responsiveness;
- no duplicate workflow execution occurs.

### Design B — Explicit terminal-output projector

Keep streaming, but add a projector that:

- understands actual workflow output event metadata;
- identifies terminal outputs through a supported public API or a CanDoItAll-owned descriptor created when the workflow is built;
- preserves intermediate updates for activity only;
- returns terminal updates as the authoritative response;
- falls back to all updates only when no terminal output exists;
- uses MEAI merging without custom time sorting.

Use only if terminal executor identity can be obtained without reflection or opaque JSON parsing.

### Design C — Workflow-owned final response event

Configure the workflow to emit one unambiguous final response/output event and make that event the application contract, while intermediate participant events feed activity.

This may be viable if the builder and process definitions can guarantee one final terminal channel.

### Rejected design

- Execute streaming for activity and then execute non-streaming again for the result. This would duplicate model/tool work and mutations.

## Depth Guard Placement

The max handoff depth must remain effective even if response projection changes.

Preferred order:

1. enforce at the handoff tool/workflow transition boundary using run-scoped state;
2. otherwise observe supported workflow events with a run-scoped counter;
3. keep the existing streaming observer only if it does not force the authoritative response to be rebuilt incorrectly.

Tests must cover:

- repeated call ID;
- missing call ID;
- same handoff tool emitted in multiple update fragments;
- concurrent sessions;
- return-to-previous;
- max depth exactly reached;
- max depth exceeded before a mutation;
- cancellation and disposal after the exception.

## Message Ordering

Validate both:

- caller-visible final response;
- workflow chat history persisted by MAF.

A fix in internal history does not guarantee the separately merged caller-visible response is correct.

Do not sort updates by timestamp. Timestamps may be absent, duplicated, or provider-assigned. Preserve first-observed event order and use stable IDs only for grouping, not global ordering.

## Structured Output and Finalizers

After terminal projection is corrected:

- compare required-finalizer success/failure frequency;
- confirm typed output still comes from the authoritative finalizer invocation where required;
- confirm a workflow terminal output is not mistaken for finalizer output;
- keep finalizer sequence validation;
- remove only code proven to compensate for a lost/intermediate workflow response.

## Acceptance Rule

For each fixture, one documented response projection is authoritative. Activity updates may contain more information, but they must not alter the machine output or final user-visible result.
