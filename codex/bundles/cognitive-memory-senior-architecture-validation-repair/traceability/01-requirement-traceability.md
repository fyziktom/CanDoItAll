# Requirement Traceability

| Requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| SR-001 | `analysis/01-current-state.md` | `01-01-query-shape-and-architecture-repairs` | Bundle validator output for both prior bundles | Prior completion claim validated structurally, not blindly accepted. |
| SR-002 | `analysis/01-current-state.md` | `01-01-query-shape-and-architecture-repairs` | Performance and EF scan counts | Scan scope is Cognitive Memory module plus API route file. |
| SR-003 | `analysis/01-current-state.md` | `01-01-query-shape-and-architecture-repairs` | Current-state findings and residual risks | Large-file splits are separated from current defects. |
| SR-010 | `subbundles/01-01-query-shape-and-architecture-repairs/README.md` | `01-01-query-shape-and-architecture-repairs` | Targeted recall tests and code inspection | No public API or schema change. |
| SR-011 | `subbundles/01-01-query-shape-and-architecture-repairs/README.md` | `01-01-query-shape-and-architecture-repairs` | New signal query regression test | Critical for recency-sensitive agent memory signals. |
| SR-012 | `subbundles/01-01-query-shape-and-architecture-repairs/README.md` | `01-01-query-shape-and-architecture-repairs` | `CognitiveMemorySignalLedgerTests` | Test proves newer valid signal survives paging. |
| SR-020 | `subbundles/02-02-memory-api-quality-validation-and-closure/README.md` | `02-02-memory-api-quality-validation-and-closure` | API status and recall smoke | If blocked, exact endpoint diagnostic must be recorded. |
| SR-021 | `subbundles/02-02-memory-api-quality-validation-and-closure/README.md` | `02-02-memory-api-quality-validation-and-closure` | Recall context compared with source truth | Uses focused source-backed query. |
| SR-022 | `reviews/01-execution-report.md` | `02-02-memory-api-quality-validation-and-closure` | API response inspection | Secret/router content must not appear. |
| SR-023 | `reviews/01-execution-report.md` | `02-02-memory-api-quality-validation-and-closure` | Snapshot API default and explicit-history proof | Default snapshot returned 0 resolved review items; explicit flag returned history. |
| SR-024 | `tests/CanDoItAll.Tests.Unit/CognitiveMemoryRecallOrchestratorTests.cs` | `02-02-memory-api-quality-validation-and-closure` | English-to-Czech recall regression and live LB4U pricing recall | Final live recall selected `LB4U-BP.docx (6)`. |
| SR-025 | `tests/CanDoItAll.Tests.Unit/CognitiveMemoryRecallOrchestratorTests.cs` | `02-02-memory-api-quality-validation-and-closure` | Contact-line redaction regression and live recall check | Final live recall contained no email or `+420` phone pattern. |
| SR-030 | `reviews/01-execution-report.md` | `02-02-memory-api-quality-validation-and-closure` | Completed execution report | Raw notes closed one by one. |
| SR-031 | `reviews/01-execution-report.md` | `02-02-memory-api-quality-validation-and-closure` | Prepared and completed validator output | Final closure blocker if validator fails. |
