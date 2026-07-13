# Process Flow And Target Protocol

## Runtime Protocol

1. Launch compiles process plan, artifact connections, and durable step contracts.
2. Runtime persists expected connected input lineage for every step before execution.
3. Scheduler marks a step ready only when dependencies are terminal and all required connected input packages are satisfiable.
4. Dispatcher claims the step and provides the driver a bounded input package.
5. Driver executes the step and exposes a current-step contract retrieval tool to the assigned agent/finalizer.
6. Finalizer submits evidence and output refs through a typed completion channel.
7. Runtime finalization gate evaluates generic requirements and driver-supplied policy facts.
8. Runtime records accepted outputs or recovery route.
9. Manager handoff confirms or repairs when required before downstream scheduling.

## Recovery Protocol

| Failure fact | Owner | Route |
|---|---|---|
| Missing connected input artifact | Upstream producer or manager | Rework producer step selected from lineage, or manager unresolved-lineage action. |
| Missing current-step produced artifact | Current step | Retry only if inputs/access are satisfied and operation is idempotent. |
| Missing required tool receipt because tool was skipped | Current step | Current-step repair if idempotent and tool is available. |
| Missing tool or denied access | Manager | Grant, reassign, or terminal policy block. |
| Transient provider/runtime timeout | Current step/runtime | Safe retry only with idempotency and retry budget. |
| Invalid artifact connection in template | Template/plan owner | Fail before or block launch; do not retry agent. |
| Unknown failure | Manager | Manager-required diagnostic with evidence. |

## Handoff Protocol

- Handoff is not a prompt phrase. It is a runtime state or receipt.
- A finalization-required step cannot make consumers ready until finalization is accepted.
- A manager-confirmation-required step cannot make consumers ready until manager confirmation is recorded.
- Manager confirmation must cite finalization receipt id and any repaired artifacts or access decisions.
- Downstream packages must be rebuilt from durable runtime facts after handoff.
