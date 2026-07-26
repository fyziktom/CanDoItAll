# Normalized Requirements

| ID | Requirement | Observable acceptance |
| --- | --- | --- |
| R01 | Persist one dedicated compact record for every canonical completed, failed, or cancelled process run; reserve the typed `Escalated` disposition for a future explicit ending transition. | Replaying a terminal event is idempotent. `ManagerLoopBudgetEscalated` remains an active attention event and never creates a false terminal record. |
| R02 | Store queryable scalar identity and timing fields plus strongly typed JSON hard facts. | Record exposes run/root/parent/definition/version/project IDs, disposition, timestamps/duration, step/attempt/repetition totals, executor/agent/workflow/subprocess IDs, token/cost/tool totals, result/artifact references, and schema version. |
| R03 | Represent evidence quality explicitly. | Missing start time, usage, pricing, assignment, or Agent Framework evidence is surfaced through typed completeness flags; values are never fabricated. |
| R04 | Keep historical reads join-light and privacy-aware. | EF entity has no navigation relationships; list/filter/order uses indexed scalar columns; prompts, log bodies, tool arguments, and secrets are excluded from compact payloads. |
| R05 | Generate a structured manager narrative asynchronously. | Hard facts are available before narrative completion; narrative has Pending/Generating/Completed/Failed states, attempts, timestamps, masked actionable error state, and retry/lease behavior. |
| R06 | Keep domain and integration boundaries clean. | Runtime has no dependency on Projections or Agent Framework; Application owns finalization orchestration; Modules.Processes adapts Agent Framework and manager selection; Persistence owns EF implementation. |
| R07 | Make records the default reusable history source. | Runs, summary detail, Graphs, Analytics, manager/CRM integration seams, and terminal project-structure node can consume the record without canonical deep hydration. |
| R08 | Keep deep detail explicit. | Existing event/state/assignment/Agent Framework evidence loads only on an explicitly named detail/evidence path and never per row in a normal history list. |
| R09 | Remove identified avoidable I/O. | GET routes no longer synchronously replay projection writes; record reads are bounded before deserialization/hydration; Agent Framework summary enumeration is batched and details are optional; workspace history/metrics are not redundantly loaded. |
| R10 | Expose typed Processes APIs. | API provides bounded/filterable run-record list, per-run summary, and analytics endpoints with documented not-found/validation behavior and cancellation support. |
| R11 | Provide a safe persistence rollout. | Additive EF migration creates the table and indexes; schema/payload versions support future evolution; backfill/rebuild path is idempotent and marks incomplete evidence. |
| R12 | Update the authoritative Processes API skill. | `C:\repositories\CanDoItAll.SharedInfo\codex\skills\candoitall-api-processes\SKILL.md` matches implemented commands, routes, query semantics, readback, and validation. |
| R13 | Preserve modularity. | Run-record contracts/query/assembly/generation are cohesive top-level types; no new partial class and no unrelated abstraction or refactor is introduced. |
| R14 | Close with measurable proof. | Two-pass performance review, focused unit/integration tests, migration/model checks, solution build, architecture gate, and final bundle validator are recorded. |
