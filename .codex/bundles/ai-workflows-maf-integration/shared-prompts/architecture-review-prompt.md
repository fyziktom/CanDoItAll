# Architecture Review Prompt

```text
Perform an architecture review for the completed phase.

Review against these invariants:
- Processes remain above workflows and agents.
- Workflow domain models are distinct from process and MAF runtime models.
- Public API and persistence contracts do not leak raw MAF types without an explicit accepted decision.
- Executor kinds, node kinds, component kinds, run states, event kinds, and artifact kinds are strongly typed.
- MAF workflow execution primitives are used deliberately, with CanDoItAll owning durable run management.
- Durable production/long-running execution evaluates and prefers MAF DurableTask/DTS rather than reimplementing durable orchestration.
- In-process execution is limited to local development, previews, tests, or approved short non-durable runs.
- Hosting chooses `ConfigureDurableOptions` for agents plus workflows or documents why workflow-only `ConfigureDurableWorkflows` is correct.
- Azure Functions generated endpoints and MCP exposure are either integrated with product authorization/audit or explicitly rejected.
- Human-in-loop, checkpoints, artifacts, cancellation, resume, and observation are explicit durable concepts.
- Runtime/API hot paths avoid sync-over-async, replay-unsafe orchestration logic, and avoidable allocation-heavy event/status processing.
- UI changes follow existing Blazor/component patterns and do not duplicate process logic blindly.

Record:
- Blocking findings.
- Non-blocking findings.
- Required edits before next phase.
- Accepted tradeoffs.
- Decision on whether the next phase may proceed.
```
