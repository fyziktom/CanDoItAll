# SB38 Proof Manifest

## Status And Scope

- Status: Completed; SB40 added fenced PostgreSQL leases and passed terminal browser/full-system validation.
- Requirements: R01, R03, R04, R07, R12, R13, R16, R20, R26-R28.
- Semantic contract: `bundle://proof/SB38/semantic-invariants.md`.

## Artifact Index

- Failing-first N/A for this process reconstruction: no production pre-change executable transcript set was retained and none is fabricated.
- Passing focused builds/tests: `bundle://proof/SB38/transcripts/reported-focused-validation.txt`.
- Passing terminal worker/browser confirmation: `bundle://proof/SB40/transcripts/terminal-validation.txt` and `bundle://proof/SB40/transcripts/browser-validation.txt`.
- Source/partial/anti-stub/risk audit: `bundle://proof/SB38/transcripts/source-and-anti-stub-audit.txt`.
- Before/after SHA-256 anchors: `bundle://proof/SB38/transcripts/file-hashes.txt`.
- Browser/screenshots: `bundle://proof/SB40/transcripts/browser-validation.txt`.
- Representative SHA-256 after hash: 7a49b92f93062d328be91436e7cb203fe4d328a7e2f78de230d32961210d215b.

## Semantic Adequacy

- Shallow-pass trap: split files but keep partial god drivers, save only known UI fields, persist raw secrets, or advertise operations with no lifecycle.
- Positive: official remote MCP path, strict HTTP/MCP settings, focused transport tests, lossless provider editor tests, and hosted-worker tests passed with reported aggregates.
- Negative: malformed headers/env refs, raw credential migration, unsupported operations, timeout/cancellation, and provider editor preservation are covered by focused reports.
- Terminal lease result: PostgreSQL now uses atomic owner/token-fenced phase leases with renewal/expiry; InMemory remains explicitly process-local and workers default disabled.
- Proof-depth disclosure: no complete pre-change transcript set was retained; SB40 used fresh lease negatives and browser/full-system proof.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Typed transport request/configuration | HTTP/MCP factory/codec hashes in `bundle://proof/SB38/transcripts/file-hashes.txt` | HTTP/MCP invokers and drivers | 27/27 + 12/12 reports | HTTP012-HTTP014 and malformed/unsupported focused cases |
| Provider profile edit | Module Memory codecs/editor hashes | provider registry persistence path | 26/26 focused tests | raw credential and preservation negatives |
| Worker cycle | hosting service/cycle hashes and `repo://src/Memory/CanDoItAll.Memory.Persistence/PostgreSqlMemoryWorkerLeasePersistence.cs` | operation/feedback/event/retention processors | 17/17 focused tests plus SB40 lease tests | owner/token fencing and lease expiry negatives; catastrophic expiry remains at-least-once |

## Closure Decision

PASS. SB40 closed browser/full-system proof and the distributed-lease disposition.
