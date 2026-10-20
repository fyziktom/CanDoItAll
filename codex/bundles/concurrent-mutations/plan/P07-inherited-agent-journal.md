# Inherited Agent planning admission

Refinement after the active Process material scope. Implement the inherited journal and actual owner path before lifting any Process executor refusal.

The journal, live owner and actual MAF input path now have
[242 distinct passing behaviors](../validation/2026-10-19-inherited-agent-planning.md)
across an explicit test-only repair bridge. Actual Process/Workflow admission, inherited
Workbench publication and manager continuation below remain implementation work; the
ordinary executor refusal has not been lifted.

## Protocol and provenance

Journal schema 7 and planning decision schema 2 explicitly distinguish an inherited background decision from schema 6 interactive planning. Keep schema 1 planning bytes and interactive session, latest-run, approval and publication snapshot checks unchanged. A persisted planning journal cannot change between these origins.

The new Agent-owned origin records its original profile, workspace namespace, Agent, background source binding, bounded opaque owner binding, material identity, original capture timestamp, semantic digest, original parent policy and exact unsettled child work grant. Agent Models/Core do not reference Process or Workbench. The module adapter interprets the owner binding and verifies it against the canonical Process enrollment; an envelope is not authority by itself.

Do not fabricate Workspace/Agent provenance for an inherited policy. The decision has exactly one provenance: the existing interactive record or the explicit inherited origin. The inherited local policy keeps one basis for the lifetime of that Agent occurrence, even when the parent captured a StepSnapshot. Publication freshness and maximum age come from the parent; local work comes from its finite grant; local replans are zero. Changed material is returned to the parent for its Fail/Pause/Replan decision. Both explicit and automatic local refresh refuse inherited origins.

Only one bounded basis payload is retained in the Agent journal after exact canonical material lookup. The origin retains identity/digest/time, not another payload. Background input contains the original instruction, not an additional material copy. Each provider iteration uses the same retained basis; recovery has no current-source fallback. Parent capture time may precede child start and remains the age origin. Future, substituted, missing or expired material fails before provider entry.

## Startup, recovery and effects

The Process owner enrolls and funds one exact child occurrence under the root fence. The adapter uses that occurrence as the actual execution ID and performs exact lookup before preparing input or a journal. Existing child input, metadata, source, policy, contract and grant must match. An admitted child or unknown outcome never receives another ID or grant on retry.

Background planning acquisition must use a separate explicit path with current source policy and bound inherited origin. Keep interactive preparation and saved actions inaccessible to it. Reuse the pure step/work transitions and provider adapter, not the interactive coordinator or a synthetic chat. No new runtime partial, history enumeration or shadow reservation index is justified.

Before actual publication, the Workbench adapter acquires original source/Project/catalog leases, then the effect transaction and Process root fence. It validates the exact child generation, shared active scope, current read/write authority and finite active work. Journal acknowledgement remains outside SQL; a lost acknowledgement resolves the original business receipt. Manager continuation requires the actual waiting decision and current manager authority, not a revived old claim.

Unknown provider/effect outcomes retain grants and block refresh. Accepted effects remain historical. Parent refresh creates a new stable child occurrence after complete quiescence; it never rewrites the old journal.

## Verification and capacity

Test old bytes and schema downgrade refusal, source/profile/grant substitution, inherited time and expiry, first capture and repeated provider iterations, no local refresh, immutable origin across persisted updates, exact-ID restart and lost acknowledgement, actual inherited provider input and parent-fenced effect races. Keep ordinary chat tests as regressions.

Measure one bounded payload per child, provider output/journal growth, exact lookup and effect lock duration separately. Per-record bounds alone do not qualify 64 children times 32 generations. Actual Process/Workflow producer activation and settings remain closed until the full owner path and capability negotiation pass.
