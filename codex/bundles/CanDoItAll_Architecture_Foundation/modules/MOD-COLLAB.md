# MOD-COLLAB — Collaboration: discussion and human cooperation

**Target owner:** Collaboration

Threads, participants, messages and inbox state with their own collaboration semantics. They are not agent execution transcripts or Simple Chats merely because all contain text messages.

**Repository discovery location:** `src/Modules/CanDoItAll.Modules.Collaboration`

**Primary sources:** SRC-020, MAP-11

## Provided responsibilities

- Thread/message queries and commands, participant membership and inbox/read state.
- Safe object references and any explicitly supported cross-module activity feed.
- Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

## Required capabilities

- Identity/party summaries and current authorization; owners of referenced objects.
- Shared presentation primitives without inheriting execution state.

## Forbidden shortcuts

- Do not merge all chat types into one business table for UI reuse.
- Discussion content is not an implicit agent system prompt.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: verify current membership/privacy semantics before moving contracts.
- ESTIMATE: real-time collaboration, mentions and richer attachments; do not claim these are all delivered.

## Persistence

Collaboration owns messages and membership. Project/task links have explicit lifecycle handling rather than indiscriminate cross-domain EF cascades.

## Recorded capabilities

- **FEAT-085** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Collaboration UI concepts and runtime.
- **FEAT-086** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Threads, participants, messages, and inbox services.
- **FEAT-087** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Collaboration shell/page integration and object links.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-050 — Collaboration thread/message:** declaration owner, implementation/integration boundary.
- **CON-056 — Database profile transfer participant protocol:** implementation/integration boundary, extension adapter.
- **CON-074 — Collaboration message contributions:** declaration owner, implementation/integration boundary.

## Proof and unresolved questions

Relevant planned scenarios: QA-070, QA-156.

Related gaps: GAP-022.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
