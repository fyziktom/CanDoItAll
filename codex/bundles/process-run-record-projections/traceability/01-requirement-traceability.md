# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Actual proof / closure evidence | Notes |
| --- | --- | --- | --- | --- |
| R01 dispositions/idempotent record | `requirements/01-normalized-requirements.md` | SB02, SB03 | `EfProcessRunRecordStoreTests` plus direct `ProcessRuntimeProjectionProjector` lifecycle tests | Only canonical ending events seed records; manager-loop escalation is explicitly excluded. |
| R02 hard facts | same | SB02, SB03 | `ProcessRunRecordAssemblerTests` completed/failed/cancelled/subtree fixtures | Includes subprocess, repetition, event, execution, token, cost, tool, and artifact totals. |
| R03 completeness | same | SB02, SB03 | missing-evidence and cap-boundary assembler/reader tests | No invented values; bounded truncation marks evidence missing. |
| R04 join-light/privacy-aware | architecture ADR-01/02 | SB02 | migration/model inspection, store query tests, and aggregator/API negative privacy assertions | Participant index is denormalized without FK; generated bodies are excluded. |
| R05 async manager narrative | architecture ADR-03 | SB03 | narrative generator, store lease, and batch processor success/failure/reuse/deferral tests | Never blocks facts or a read path. |
| R06 boundaries | architecture boundary/dependency maps | SB02-SB06 | solution build, manual project-reference inspection, and final architecture gate | CodeAnalytics unavailability is recorded with compensating controls. |
| R07 reusable consumers | target solution | SB03, SB04 | project-node purge integration plus workspace/dashboard/cost/query tests | CRM can consume the application/API record seam without a new dependency. |
| R08 explicit deep detail | target solution | SB04 | list-only throwing fakes, compact payload tests, and explicit detail-level reader tests | Legacy operational diagnostics remain separate. |
| R09 I/O reduction | analysis current state | SB01, SB04, SB06 | call-count assertions and the repeated 13-file performance scan | No unsafe shared-`DbContext` parallelism. |
| R10 typed APIs | requirements | SB04 | compiled `ProcessRunRecordsApi`, query-service tests, API serialization tests, and integration-host build | HTTP execution is environment-blocked by unavailable Docker/PostgreSQL and is not counted as a live pass. |
| R11 rollout/backfill | assumptions/risks | SB02, SB03 | additive migration, `has-pending-model-changes`, idempotent/freshness backfill tests | Backfill materialization freshness is distinct from terminal time. |
| R12 SharedInfo skill | input source artifacts | SB05 | source-to-skill route readback and sibling-repository `git diff --check` | Authoritative skill was updated successfully. |
| R13 modularity | architecture ADR-06 | SB02-SB06 | top-level cohesive types, extracted project record adapter, direct tests, no new partials, and architecture gate | No new project dependency or generic repository. |
| R14 closure proof | structured input evidence contract | SB06 | solution build, focused suites, EF drift check, diff checks, architecture gate, and completed-stage validator | Exact results are recorded in the execution report. |
| N001-N009 raw notes | `inputs/02-structured-input.md` | SB01-SB06 | execution-report raw-note closure table | Every note is marked solved with source/test proof and residual limits called out separately. |
