# SB38 Semantic Invariants

## SB38-INV-01 - Context And Configuration Are Lossless And Secret-safe

- Expected: typed runtime identity reaches transport; editing preserves unrelated profile data; only credential references persist/render.
- Shallow implementation: rebuild a subset of extensions or store/display raw keys.
- Evidence: `bundle://proof/SB38/transcripts/reported-focused-validation.txt` and hash anchors.
- Negative: invalid header/env name, CR/LF secret, missing reference, raw credential save.
- Downstream: SB40 verifies real-host save/reopen and outbound context.

## SB38-INV-02 - Transport Claims Match Real Behavior

- Expected: non-partial collaborators, explicit MCP/HTTP registration, local rejection of unsupported operations.
- Shallow implementation: descriptors/manifest flags without an invoker/status lifecycle.
- Evidence: MCP 27/27 + 12/12, HTTP build 0/0, and source audit.
- Negative: malformed response, unavailable/timeout/cancel, unsupported capability with zero fallback.

## SB38-INV-03 - Hosted Workers Do Not Hide Failure Semantics

- Expected: configured host runs bounded phases and isolates per-item/per-phase failures.
- Shallow implementation: DI registration without a hosted loop or one poison item starving all work.
- Evidence: worker hosting 17/17 and hosting source hashes.
- Terminal disposition: SB40 added proven PostgreSQL owner/token-fenced phase leases; InMemory remains explicitly process-local.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| Transport envelope | AgentFramework.Memory/request factory | HTTP/MCP invoker | operation ledger/response mapper | missing context/config/unsupported cases |
| Worker item | durable stores/processors | hosted cycle | retry/dead-letter/retention | poison-item and lease risk |

## Validator Invariant Contract

- Invariant ID: SB38-TRANSPORT-WORKER-INTEGRITY
- Source raw note: transports and helpers must have proper ownership, preserve typed context/settings, and not advertise unsupported behavior.
- Expected behavior: HTTP/MCP collaborators are cohesive, credentials are environment references, profile edits are lossless, response/lifecycle capabilities are truthful, and PostgreSQL worker phases are leased.
- Disallowed shallow implementation: partial drivers, raw secrets, destructive profile saves, unhosted worker claims, or process-local locks presented as distributed.
- Failing-first test: failing-first N/A for this process reconstruction because the complete pre-change command set was not retained; no baseline is fabricated.
- Passing test: bundle://proof/SB38/transcripts/reported-focused-validation.txt and bundle://proof/SB40/transcripts/source-and-architecture-audit.txt.
- Changed source files: repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderResponseReader.cs, repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderResponseGuard.cs, and repo://src/Memory/CanDoItAll.Memory.Persistence/PostgreSqlMemoryWorkerLeasePersistence.cs.
- Production assertions: bounded responses precede mapping, raw credentials fail, unsupported actions remain hidden/typed, and lease owner/token fencing controls cross-replica phases.
- Red-team negative case: malformed header/env refs, oversize bodies/results, unsupported actions, duplicate lease owners, and expiry are rejected.
- Downstream dependency check: SB39 used the process-ready transport seam and SB40 completed browser/live/full-suite proof.
