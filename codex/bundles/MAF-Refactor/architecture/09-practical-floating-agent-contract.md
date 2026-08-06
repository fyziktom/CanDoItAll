# Practical floating-agent behavior contract

## Core rule

The floating chat follows the current UI **between turns**. Each admitted turn is immutable **during execution and continuation**.

## Two independent clocks

### UI observation clock

Advances when:

- route/navigation identity changes;
- Project Structure switches Canvas/Gantt/Calendar/Manager Summary;
- selection changes;
- Gantt projection/view state changes;
- the context becomes loading, partial, failed, or unavailable;
- the database profile generation changes.

### Conversation clock

Advances when:

- a user explicitly sends a message;
- the application admits a turn;
- a context binding is adopted/detached;
- a context epoch changes;
- an approval decision continues an existing turn.

Navigation may advance the UI clock without advancing the conversation clock or invoking a model.

## Admission algorithm

```text
1. Capture one route-fenced UI observation.
2. Read the chat's last accepted context binding revision.
3. Classify the transition.
4. Resolve canonical authority for the requested source.
5. Reject source/scope/generation mismatches.
6. Compose bounded model context.
7. Create a context epoch/reference/digest.
8. Lease opaque attachments to the execution run.
9. Persist the safe reference and authority fingerprint.
10. Invoke the runtime through the narrow execution port.
```

## Context transition table

| Previous | Current | Transition | Epoch | Authority |
|---|---|---|---|---|
| Project X Canvas | Project X Gantt | `ViewChanged` | keep | revalidate X |
| Project X Gantt task A | task B | `SelectionChanged` | keep | revalidate X |
| Project X | Project Y | `SourceEntityChanged` | new | resolve Y |
| Project Structure X | CRM account A | `SourceKindChanged` | new | resolve CRM A |
| Follow current | Detached | `ContextDetached` | new | no context-derived authority |
| Ready | unavailable/loading | `ContextUnavailable` or partial observation | explicit | never reuse stale observation silently |

## Trusted model header

The turn composer should create a bounded system/context section similar to:

```text
Current application context
- Context epoch: 8f...
- Source: Project Structure / Project X
- Current surface: Project Structure
- Current view: Gantt
- Transition since the previous accepted turn: Canvas -> Gantt
- Prior UI facts from earlier context epochs are historical and must not be treated as current.
- Use authorized product tools for exact current data and mutations.
```

This is application-generated context. It is not a user message and is not itself an authorization grant.

## Gantt observation contract

The Gantt contributor should publish bounded visible/projection facts, not the full project graph:

- project identity and active Gantt view;
- projection content/revision fingerprint;
- task/dependency/milestone/unscheduled counts when available;
- projected date range;
- bounded issue/warning summary;
- selected task/row or visible range when exposed by the component;
- row-order/view-state fingerprint;
- completeness: ready, partial/loading, failed;
- captured/fresh-until timestamps.

Exact task data is read through canonical Project Structure tools/services. Mutations use typed product commands/tools and readback evidence.

## Concurrency rules

- Strict capture returns either a coherent old snapshot or a coherent new snapshot, never a mixture.
- Binding updates use expected revision or compare-and-swap.
- A failed admission does not claim that the conversation adopted the new source.
- Provider execution is never retried merely because navigation raced with Send.
- Completion refresh refers to the originating context source and is ignored or safely routed when the current UI no longer matches.

## Approval rules

- Every proposal identifies the execution run and original turn context.
- Approval UI may be displayed while the user views another project.
- Decision maps by stable proposal/approval ID.
- Continuation restores original context, authority, provider, model, toolset and runtime-state envelope.
- Current UI context is not consulted.

## User-visible behavior

Recommended floating-chat indicators:

- `Following: Project X / Gantt`
- `Current run context: Project X / Canvas`
- `Next turn context changed to: Project X / Gantt`
- `Detached from application context`
- `Context is loading; exact visible Gantt facts are partial`

These indicators improve trust and make context mistakes diagnosable without exposing raw authority details.
