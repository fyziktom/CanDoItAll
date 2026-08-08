# Architecture summary

## Refined conclusion about floating agents

The current code already has the correct **turn immutability** primitive:

- the active UI module publishes a revisioned context;
- Send captures one strict snapshot;
- the run stores a digest;
- the exact transient object remains leased for approval continuation;
- navigation after admission does not change the admitted run.

The missing architecture is not “make the runtime always read the latest UI.” That would be unsafe. The missing architecture is a controlled join between two independent timelines:

```text
Conversation timeline:  Turn 1 -------- Turn 2 -------- Turn 3
UI timeline:            Canvas -> Gantt -> Project Y -> Calendar
```

At each explicit Send, the application joins the current UI observation with the chat's previous context binding and creates one immutable turn context. It also independently resolves what the turn may do.

## Required state separation

```text
Canonical product state
  Project Structure / Processes / other owning modules
              |
              +----> UI projection (Canvas, Gantt, selected row)
              |           |
              |           +----> live UI observation registry
              |                         |
Conversation binding -------------------+----> immutable turn capture
                                                   |
Canonical authorization --------------------------+----> execution authority
                                                   |
                                                   +----> execution run
                                                             |
                                                             +----> MAF adapter state envelope
```

The UI observation tells the model what the user is looking at. It does not grant file, project, process, or mutation rights. The authority resolver determines those rights from canonical services.

## Practical semantics

### Same project, different view

Project X Canvas -> Project X Gantt:

- same chat session;
- same context epoch;
- `ViewChanged` transition on the next turn;
- current Gantt facts supplied;
- Project X authority revalidated;
- no provider call merely because the tab changed.

### Different project

Project X -> Project Y:

- same transcript may remain;
- new context epoch;
- `SourceEntityChanged` transition;
- trusted context header marks old Project X UI facts as historical;
- new Project Y authority is resolved;
- an old Project X run or approval remains Project X.

### Running and waiting turns

A run is bound to the context and authority captured at admission. Later navigation only affects the next turn. Approval continuation never recaptures the current surface.

## Why the MAF boundary must change

MAF should receive a complete runtime-neutral execution request and return runtime-neutral results/evidence. It should not:

- decide product or process authority;
- read process artifacts;
- know process status/path semantics;
- build workspace services by looking into the root container;
- infer project scope from workflow payloads;
- own provider diagnostics and model administration through the same broad execution interface.

The bundle therefore combines the floating-context work with scope, runtime-port, dependency-direction, and process-ownership refactors. Doing only the UI portion would preserve the current authority ambiguity.

## Revision 2 additions

This revision adds a safe migration architecture, not a different target architecture. The main additions are:

- complete affected call-chain and DI/persistence/API impact maps;
- single-path strangler cutovers and rollback boundaries;
- a dedicated stabilization/bugfix subbundle before deletion;
- owner-stage diagnostics and bounded correlation;
- provider-backed lightweight LLM invocation below agent execution;
- a future ordinary-chat application boundary above the stateless LLM port;
- Claude Code/Fable 5 prompts and durable model handoffs.

The key implementation insight is that the lightweight LLM port should reuse the existing provider runtime and SDK-neutral chat drivers rather than wrapping `MafAgentRuntime` again.
