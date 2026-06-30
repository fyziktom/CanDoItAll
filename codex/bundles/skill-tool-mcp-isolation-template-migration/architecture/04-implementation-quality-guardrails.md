# Implementation Quality Guardrails

## Refactoring Gate

The migration must not create new large files that reproduce the current MAF coupling under new project names. SB05, SB07, and SB09 must explicitly review file size, type ownership, dependency direction, and testability before dependent phases start.

## File And Type Rules

| Rule | Gate |
| --- | --- |
| New implementation files over 500 lines require an explicit split plan or accepted-risk note in the subbundle proof. | SB05, SB09 |
| New methods over 80 lines require extraction unless the proof explains why keeping the method whole is clearer. | SB05, SB09 |
| Capability descriptor DTOs must not be private nested MAF records. Shared descriptors belong in abstraction/template projects. | SB01, SB08 |
| No giant switch over capability keys for active runtime behavior. Registries, descriptors, or typed implementation keys must drive dispatch. | SB02, SB08 |
| No raw string identifiers for tool names, capability keys, MCP server keys, operation classifications, or policy categories outside compatibility constants or generated registries. | SB01-SB12 |
| Classes that are leaf services should be sealed unless tests/proxies require otherwise. | SB05, SB09 |
| New public abstractions require a real boundary: test seam, external adapter, transport boundary, or independent implementation. | SB01-SB05 |

## Dependency Rules

| Project area | Must not reference |
| --- | --- |
| Capability abstractions | MAF, Blazor, EF/persistence implementation, concrete MCP SDK clients unless hidden behind contracts. |
| Skills implementation | MAF and Blazor. |
| Tools implementation | MAF and Blazor. Existing feature services must be consumed through narrow interfaces. |
| MCP implementation | MAF and Blazor. |
| Template loader | MAF and UI. Persistence can consume loader output, but loader must not own persistence writes. |
| MAF adapter | Template file parsing and seed materialization internals. |
| UI/API | Concrete process launching or MCP lifecycle internals; call setup-test services instead. |

## Performance Review Checklist

Run the scoped performance scan from `analysis/03-codeanalytics-and-performance-review.md` at SB05 and SB09. The review must address:

- Async correctness: no sync-over-async, no `async void`, cancellation flows through external calls and MCP startup.
- Allocation pressure: avoid repeated template parsing, repeated JSON options creation, per-call static collection creation, and unnecessary LINQ in capability call dispatch.
- Collections: pre-size materialized descriptors when count is known, use ordinal comparers, and use read-only/frozen lookup tables for static registries where appropriate.
- Serialization: cache `JsonSerializerOptions` or use source-generated contexts for template/setup result DTOs when feasible.
- I/O: use async file/process/HTTP APIs, bound output, and do not read unbounded process streams into memory.
- Runtime cleanup: MCP process/service ownership must be deterministic and testable.

## Proof Rules

Checkpoint proof must include:

- Static search summary for hardcoded identifiers, fallbacks, oversized files, and direct MAF dependencies.
- Unit and integration test list that proves unhappy paths, not just successful load/call paths.
- Diagnostics samples for one template validation failure, one external tool failure, and one MCP setup failure.
- Accepted-risk table for any performance or size finding intentionally deferred.
