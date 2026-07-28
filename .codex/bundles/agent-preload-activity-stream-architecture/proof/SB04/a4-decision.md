# SB04 A4 Module Snapshot Decision

## Decision

`PASS`

Date: `2026-07-27`

SB05 progression is authorized.

## Why the gate passes

- Project Structure captures a bounded, redacted, immutable held-surface attachment.
  Mapping is pure and covered invocation dispatch performs zero persistence calls.
- Publication revision, content/selection fingerprint, coverage fingerprint,
  database-profile generation, and freshness fingerprint retain separate meanings.
- Expired, profile-mismatched, unavailable, ineligible, and coverage-insufficient
  snapshots fail explicitly. Only an explicit `CanonicalCurrent` request performs the
  deeper read.
- The registry publishes contributor fragments and concrete typed attachments
  atomically. Concurrent capture observes one complete old or new publication, and an
  invocation keeps the exact captured envelope after later UI changes.
- Workbench/Processes own their concrete snapshots. Core transports
  `IAgentChatContextAttachment` opaquely without module references, string keys, or
  object dictionaries.
- Process Workspace and Live Processes publish typed provenance/freshness/coverage
  components for every emitted context field, including explicit absence.
- Context digest and approval continuation bind publication, content, coverage,
  profile, and freshness identity.
- Snapshot types and stamps are structurally absent from canonical mutation methods;
  completion can request projection refresh but cannot write captured state back.
- The parent-confirmed focused architecture unit suite passed 140/140, the selected
  component suite passed 95/95, A5 returned `GO with three P2 follow-ups`, and final
  CodeAnalytics snapshot `snap-20260728014834-63e19a8b` kept the affected project
  graph acyclic.

## Evidence provenance

The 140/140 and 95/95 totals are confirmed validation handoffs. No raw 140/140 console
transcript was retained under `proof/SB04`, so this decision cites exact source/test
surfaces in `bundle://proof/SB04/manifest.md` and does not reconstruct an original
command or console stream.

## Scope exceptions

- Process-launch preview/execute drift is not solved by this chat-preload slice.
- The incomplete `processContext` navigation handoff remains outside scope.
- Deep omitted facts require an explicit canonical-current read.
- Invocation snapshot stamps are not canonical write-concurrency tokens.

## Inherited P2s

The three A5 P2 follow-ups remain open: synchronous database-switch subscriber delay,
physical WAL/directory flush durability, and the final provider cross-host revision
window. They do not permit a hidden canonical-read fallback, stale snapshot write-back,
or a stronger distributed-consistency claim.

## Reopen rule

Reopen A4 through A7 if a captured invocation observes mixed/newer UI state, a
restricted detail escapes redaction, a covered read touches persistence, a coverage or
freshness miss silently falls back, an attachment reaches a mutation contract, or a
Process Workspace/Live Processes field lacks typed provenance.
