# Internal messages vs. Workbench nodes

## Decision
Do **not** model internal orchestration messages as default Workbench nodes.

## Why
### A node and a message have different lifecycles
A Workbench node is a user-visible, queryable domain artifact.
An internal message is an execution-plane transport envelope with concerns such as:

- delivery attempts,
- deduplication,
- correlation and causation,
- retry and dead-letter policy,
- transient subscription fan-out,
- delayed delivery,
- scheduling wakeups.

These concerns do not belong on the canonical domain graph.

### Messages would pollute the graph
If every wakeup, timeout, retry, pub-sub event, approval request, or plugin handoff becomes a graph node, the graph stops representing business artifacts and starts representing transport noise.

### Materialization should be explicit
A message can still create or update a node when it produces durable business value:

- imported email -> correspondence node,
- extracted task -> task node,
- delivery QA verdict -> validation node,
- meeting summary -> transcript / note / action node.

That is the correct conversion boundary.

## Required phase11 rule
- **Messages are not nodes by default.**
- **Nodes appear only through explicit materialization handlers.**
- **The platform must keep a separate durable internal message envelope for operational orchestration.**

## Related current-repo consequence
The current automation surface is still closer to a dashboard/read model than to a real execution plane. Phase11 must add the execution plane explicitly rather than stretching Workbench nodes into that role.
