# G01 — Accepted target and independent read lanes

Status: PLANNED, NOT EXECUTED. Critical foundation. Prerequisite: G00 written closure and exact semantic RED. Keep all new owners inside existing Module.

Create one per-panel session and one coherent read seam (or G00's explicitly proven simpler equivalent). The production adapter delegates to the registered current-profile workspace: catalog, filtered execution-run list and exact run detail. It does not copy persistence, provider-native receipt enrichment, runtime execution or approval policy.

Use explicit desired agent target (All versus requested Guid) and accepted catalog/target. Missing requested ID stays missing; no first-agent or All fallback. The page remains workspace-selection owner; host session is target resolution and request lifetime. Manual run selection is identified independently from list refresh generation. Run ID is not a new route field.

Each catalog/list/detail operation captures its request identity, target and token before the first await, installs its own task slot before invoking potentially synchronous reads, and only its current owner clears loading/error/task state. Supersede/cancel old work on changed target/run, explicit retry and disposal. Dispose owned CTS exactly once. Fence every success, failure, finally and callback even for noncooperative readers. Owner cancellation is not a visible failure. Do not use shared isBusy as request ownership.

Accept a successful list before loading detail. New agent hides previous agent rows/detail; same target can retain accepted rows while showing refresh/stale state. Initial success may choose the first current run. Explicit manual selection always wins over an older refresh. If the selected run disappears on accepted refresh, retain its unavailable identity until another user choice; do not silently pick a different run. Validate returned detail Run.Id and Run.AgentId against the captured accepted row (also in All agents). Preserve Run.Revision as read metadata without inventing mutation/concurrency writes.

A failed detail leaves usable accepted rows and a detail-only retry. Same-target failed list refresh can retain rows marked stale. Missing-run read gets a bounded unavailable message; failed read is not authoritative deletion. A successful catalog/agent resolution owns context access independently of list/detail errors. Requested unknown target is Failed; unresolved catalog is Loading/Failed. All agents publishes null only for an explicit All choice. Prevent invalid-target null callback echo from converting failure to All.

Copy public display collections at acceptance/mapping. Do not expose full SerializedSessionStateJson, MetadataJson, pending approval arguments, raw structured output or arbitrary exception text in a new UI snapshot. Apply the newly authorized G00 allowlist and UTC policy to display summaries/sections and do not redesign execution retention.

Proof: all G00 lifetime RED green, synchronous completion/echo unit tests, immutable ownership, actual registered DB-backed read filtering/detail/token cases, current page/context compatibility. Build Module and all changed owners/tests; run exact selections and portability. Close with request ownership table and removed old host state/callers. No surface movement, sandbox, production routes or approval commands.
