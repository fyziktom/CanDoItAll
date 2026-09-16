# BND-API — HTTP / transport adapters

**Target owner:** HTTP / transport adapters

Incoming authentication, DTOs/versions, authorized route scopes, ETags, Problem Details, streaming and client disconnection.

**Repository discovery location:** `None`

**Primary sources:** SRC-003, SRC-006, SRC-002

## Provided responsibilities

- Authorized HTTP/OpenAPI transport, DTO/status/ETag/problem mapping and committed-event streaming according to existing APIs.
- External client contracts with compatible routes and explicit scope/operation semantics.

## Required capabilities

- Owner application queries/commands rather than Razor pages/controllers or foreign DbContexts.
- Authentication/authorization and correct profile source authority; runtime tool grants are separate permissions.

## Forbidden shortcuts

- An existing route does not attach a runtime tool. Domain commands are shared with UI/tools; a disconnected request token does not prove rollback.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: preserve supported HTTP surfaces, routes and serialized semantics while moving services.
- ESTIMATE: expose new public operations only when owner capabilities exist; do not automatically publish every internal method.

## Persistence

Owns no business master and performs no direct domain EF writes. Transport tracking can reference owner receipts but must not create a second execution state. SSE wake-ups are not durable journals.

## Recorded capabilities

- **FEAT-109** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] HTTP and runtime tools use distinct admission authority and owner-controlled typed operations.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-002 — Agent definition commands:** caller.
- **CON-004 — Agent approval / cancellation / status:** caller.
- **CON-006 — Provider administration:** caller.
- **CON-008 — Shared-provider publish/import/sync:** caller.
- **CON-011 — CRM domain commands:** caller.
- **CON-016 — Project lifecycle orchestration:** caller.
- **CON-017 — Project hierarchy:** caller.
- **CON-018 — Structure read / invocation read:** caller.
- **CON-019 — Native structure authoring:** caller.
- **CON-020 — Structural links:** caller.
- **CON-022 — Asset binding and content lifecycle:** caller.
- **CON-023 — Canonical work-item commands:** caller.
- **CON-024 — Work assignment:** caller.
- **CON-025 — Gantt and dependency mutations:** caller.
- **CON-026 — Plan estimate/baseline commit:** caller.
- **CON-027 — Process launch:** caller.
- **CON-028 — Process run query/control:** caller.
- **CON-030 — Workflow launch/control:** caller.
- **CON-034 — Prompt authoring/curation:** caller.
- **CON-035 — Resource catalog and promotion:** caller.
- **CON-036 — TestLab plan/run/evidence:** caller.
- **CON-037 — Schedule plan and fire dispatch:** caller.
- **CON-038 — Memory operation API:** caller.
- **CON-040 — Plugin lifecycle/capability API:** caller.
- **CON-047 — Workspace preference API:** caller.
- **CON-048 — Database profile and host capability control:** caller.
- **CON-049 — Ordinary Simple Chats operations:** caller.
- **CON-050 — Collaboration thread/message:** caller.
- **CON-052 — Authorized file browsing/opening:** caller.

## Proof and unresolved questions

Relevant planned scenarios: QA-008, QA-011, QA-013, QA-053, QA-071, QA-073, QA-079, QA-081, QA-102, QA-104, QA-116, QA-119.

Related gaps: GAP-020, GAP-032.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
