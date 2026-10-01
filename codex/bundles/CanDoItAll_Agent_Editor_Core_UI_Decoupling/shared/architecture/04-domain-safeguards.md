# 4 · Domain and security safeguards retained from the Foundation

This is the UI-relevant part of the supplied Architecture Foundation, reconciled with current product instructions [S01, S02]. It does **not** replace the Foundation's unrelated domain roadmap or instruct a UI task to implement its entire contract catalogue.

## Preserve authority, not arbitrary folder structure

One authoritative owner writes each business fact. A screen can combine several owners' facts without becoming their writer. A reusable UI library and a shared DTO do not acquire ownership of those facts. Application composition wires implementations but is not another business store. Do not read or mutate a foreign aggregate by reaching directly into its DbContext to avoid a missing contract.

Keep distinctions that matter in product journeys: technical agent definition/runtime versus CRM/HR records; ordinary Simple Chats versus managed agent execution; process orchestration versus workflows; project hierarchy/presentation versus task scheduling and business assignments; file bytes/paths versus asset metadata versus project references; provider-neutral Memory contracts versus concrete provider implementation. These are semantic safeguards, not a claim that every conceptual owner already has a fully isolated project in the reviewed source.

Prefer the existing light owner contracts, bounded query/projection and command path. Where a seam is missing, extract the smallest justified closure with its owner and consumers. Do not invent a universal repository, bus, plugin system or platform-wide transaction layer as the price of moving one page.

## Context and authorization survive the UI move

Typed context identifies the actual profile/project/target/lifetime the operator saw. Treat source-read permission, ability to mention a record, permission to command its owner, delegated agent capability, runtime admission and approval as different checks. Hiding a button is presentation; enforcement remains at the owner boundary. Do not give a sandbox/test planner administrator grants to make a real-host scenario pass.

Preserve `ProjectWriteAdmission` and comparable existing admission/version tokens. A deleted/recreated entity with the same public ID is not automatically the old target. The module completion record specifically describes the project-lifetime safeguard [S05]; extend such semantics only through the relevant owner contract, not by constructing a substitute token in a renderer.

Keep secrets and confidential content out of URLs, browser-side diagnostics, screenshots and unrestricted lists. Preserve masking and failure-message sanitization, file path validation, trusted workspace roots and platform/headless restrictions. Query results and external/agent-generated content are data, not authority to perform a write.

Do not redesign JWT/API authentication, ownership of the whole application, or Blazor SSR access rules in this UI program. Preserve whatever the current production host and API policy enforce. A changed public HTTP/control-plane contract does trigger its own real-host tests, even though this program is not an API-only redesign.

## Durable operations outlive renderers

Closing a component normally ends its subscriptions and presentation effects, not an accepted workflow/process or database commit. Preserve the existing explicit cancellation semantics; do not turn navigation into an undocumented cancellation command. Keep runtime execution, approvals, workspaces, recovery and results at the existing owner boundary.

Safe retry/idempotency, receipts, transactions, outbox/inbox and compensation are backend concerns where the operation requires them. Do not claim exactly-once behavior from a disabled button or a local generation counter. Do not add a fake receipt in UI to cover an unknown outcome. The selected operation must have an honest safe path; a wider new durable protocol is a separately scoped prerequisite, not mandatory infrastructure for every read panel.

## Cross-module journeys select the proof

When a changed surface participates in a journey, test that journey's relevant boundary: agent chat over a project; prompt selection into the current chat/workflow target; file/task contribution and return; workflow/process launch, progress and result; CRM/HR assignment into a project; scheduler dispatch; copy/move/delete or export/import. Do not rerun all of these for an unrelated isolated label change, and do not substitute a sandbox click for real-owner admission.

Only the owning service changes durable links, scheduling, managed definitions and contribution lifecycles. A late UI result after delete/restore/reopen cannot recreate a record or attach output to the successor target. Preserve user edits and ownership of previously contributed content; visual row order or canvas layout is not a substitute schedule.

## Database scope

Renderer extraction should normally require no data migration. Moving contract types is not permission to rename tables, split all DbContexts, invent cross-module copies or change restore behavior. If a genuine schema/transaction change is necessary for the selected boundary, record it explicitly and apply current migration, isolated PostgreSQL and transfer/restart tests from `docs/testing.md` [S03]. Do not reuse the old integration bundle's database or environment assumptions.

Keep a compatibility/debt note for an existing backend issue outside scope. Do not accept a new unsafe cross-owner dependency merely because the complete domain redesign is deferred. This allows UI progress without freezing incorrect authority or expanding each slice into the whole Foundation roadmap.
