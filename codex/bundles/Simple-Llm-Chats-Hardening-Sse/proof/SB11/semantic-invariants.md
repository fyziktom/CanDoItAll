# SB11 semantic invariant contract

Changed-file hashes: `bundle://proof/SB11/changed-files.sha256`.
Linux commands: `bundle://proof/SB11/transcripts/01-linux-package-build.md` and
`bundle://proof/SB11/transcripts/02-focused-linux-tests.md`.

## SBI-11-01 — provider streaming is incremental and retry-bounded

- Intended behavior: OpenAI/Azure SSE and Ollama NDJSON preserve fragmented UTF-8, emit public deltas,
  and retry only before the first accepted delta; a completed-only driver is labelled
  `CompletedFallback`.
- Negative behavior: malformed frames produce stable redacted failures, and partial output prevents a
  second dispatch or canonical assistant commit.
- Proof: the Linux Unit aggregate passed every parser, adapter, operation, audit, and event case.

## SBI-11-02 — the PostgreSQL journal is authoritative

- Intended behavior: admission/finalization events share their owner transaction, sequences are unique
  across instances, retention is bounded, and transfer preserves operation/event/audit identity.
- Negative behavior: injected rollback publishes nothing; stale profile/lease owners cannot commit;
  cancellation and failed compensation cannot become success.
- Proof: the 43-case Linux Integration union passed all LLM Chat transaction, journal, lease,
  migration, transfer, and bounded-query cases against PostgreSQL 16.

## SBI-11-03 — SSE is observational and resumable

- Intended behavior: 202 admission precedes provider completion; a slow provider exposes a delta and a
  real endpoint heartbeat before completion; reconnect resumes after Last-Event-ID and closes on one
  terminal event.
- Negative behavior: disconnect does not cancel or redispatch, an expired cursor produces `stream.gap`,
  explicit cancellation remains authoritative, and provider secrets never enter failure frames.
- Proof: the real-host PostgreSQL scenario blocks the provider after `First `, observes the endpoint
  heartbeat, disconnects, resumes without duplicate text or redispatch, then exercises gap,
  cancellation, failure, and terminal closure.

## SBI-11-04 — portability proof is honest

- Intended behavior: the same package-reference graph compiles and runs on Ubuntu with explicit
  repository-root identity under isolated artifacts.
- Negative behavior: invalid configured roots fail explicitly; an unpublished dependency cannot be
  silently replaced or called CI-green.
- Proof: package mode (`UseLocalCanDoItAllLibraries=false`) builds Web with 0 warnings/errors after the
  exact clean sibling Spreadsheet source is packed at 0.1.18 inside the disposable host. A cold restore
  against nuget.org alone remains a named publication prerequisite for SB13.

## Anti-stub result

No production source changed in SB11. Real provider protocol parsers, the real Web host, the actual EF
stores/migrations, and PostgreSQL 16 were exercised. The only double replaces the billable external LLM
network boundary and deliberately controls stream timing.
