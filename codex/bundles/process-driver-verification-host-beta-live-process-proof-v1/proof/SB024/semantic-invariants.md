# SB024 Semantic Invariants

## SB024_INV_001 Durable EF Audit Boundary
- Source raw note: REQ-009 requires replacing in-memory-only audit with a durable audit boundary and query API.
- Expected behavior: the verification host appends audit records through a persistence abstraction that resolves to an EF-backed store in the full Processes module and writes `Processes_VerificationAuditRecords`.
- Disallowed shallow implementation: only retaining `InMemoryProcessVerificationAuditStore`, adding an entity without DI replacement, or adding a migration that no runtime code consumes.
- Positive proof: `Process_verification_audit_store_SB023_INV_001_persists_redacted_hashes_and_supports_queries` in `bundle://proof/SB023/transcripts/durable-audit-focused-tests.txt`.
- Migration proof: `bundle://proof/SB022/transcripts/postgresql-audit-migration-bootstrap-tests.txt`.
- Source proof: `bundle://proof/SB022/transcripts/durable-audit-entity-migration-source-assertions.txt`.
- Changed source: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationAuditStore.cs` SHA256 `c285f653d6c20bdbe8cf3b8085bee1ccb0acea323536302b0d5059e847396a87`.
- Red-team negative case: `bundle://proof/SB024/transcripts/red-team-durable-audit-shallow-proof-rejection.txt`.
- Downstream dependency check: P09 manager API work must consume the query service rather than enumerating in-memory runtime state.

## SB024_INV_002 Redacted, Hash-Preserving Query API
- Source raw note: audit records must be queryable without leaking sensitive requester input and without weakening evidence hash traceability.
- Expected behavior: audit append redacts `RequestedBy` through `ISecretRedactor`, preserves the 64-character observation hash, and query operations are typed, bounded, no-tracking, and filterable by process run, step run, and lane.
- Disallowed shallow implementation: persisting raw requester strings, recomputing or dropping the observation hash, returning unbounded queries, or accepting stringly-typed lane filters.
- Positive proof: `bundle://proof/SB023/transcripts/durable-audit-focused-tests.txt` verifies redaction, hash preservation, query-by-run/step/lane, mutation flags, and invalid limit rejection.
- Source proof: `bundle://proof/SB023/transcripts/durable-audit-redaction-query-source-assertions.txt`.
- Anti-stub audit: `bundle://proof/SB024/transcripts/gate-h-source-diff-and-anti-stub-audit.txt`.
- Changed source: `repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessVerificationAuditEntry.cs` SHA256 `e26e4d46bf88d63cd66472ef5f6aef59469ae4d56e2030816b5514bf2ddacb67`.
- Red-team negative case: `bundle://proof/SB024/transcripts/red-team-durable-audit-shallow-proof-rejection.txt`.
- Downstream dependency check: no process state mutation permissions are added by audit append/query paths.

## SB024_INV_003 Current PostgreSQL Adoption Chain
- Source raw note: durable audit must be part of the stable persistence model, including existing-schema bootstrap.
- Expected behavior: when a PostgreSQL schema already matches the current EF model but lacks migration history, bootstrap records every current migration ID, including `20260610113813_AddProcessVerificationAuditRecords`, before `MigrateAsync`.
- Disallowed shallow implementation: recording only `20260528182412_InitialPostgreSqlBaseline`, which causes later migrations to run against already-existing tables/columns.
- Positive proof: `Bootstrap_adopts_existing_postgresql_schema_without_migration_history` in `bundle://proof/SB022/transcripts/postgresql-audit-migration-bootstrap-tests.txt`.
- Source proof: `bundle://proof/SB022/transcripts/durable-audit-entity-migration-source-assertions.txt`.
- Changed source: `repo://src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs` SHA256 `926dd6bc8de1e62b4d7786cb1425a169e9ebd9e41409343757c9de725727ec89`.
- Red-team negative case: `bundle://proof/SB024/transcripts/red-team-durable-audit-shallow-proof-rejection.txt`.
- Downstream dependency check: future migrations must append to the explicit current chain if they are also represented by existing current-schema adoption.

## Production Behavior Artifact Matrix
| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessVerificationAuditEntry` / `Processes_VerificationAuditRecords` | `repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessVerificationAuditEntry.cs` and `repo://src/CanDoItAll.Modules.Processes/Persistence/Configurations/ProcessRuntimeEntityConfigurations.cs` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationAuditStore.cs` maps records to/from EF entities | `bundle://proof/SB022/transcripts/postgresql-audit-migration-bootstrap-tests.txt` and `bundle://proof/SB023/transcripts/durable-audit-focused-tests.txt` | `bundle://proof/SB024/transcripts/red-team-durable-audit-shallow-proof-rejection.txt` |
| `IProcessVerificationAuditQueryService` / `ProcessVerificationAuditQuery` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationAuditStore.cs` | `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` wires EF store/query service in module DI | `bundle://proof/SB023/transcripts/durable-audit-focused-tests.txt` proves cross-scope query behavior | `bundle://proof/SB024/transcripts/gate-h-source-diff-and-anti-stub-audit.txt` |
| Current PostgreSQL migration adoption chain | `repo://src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs` | `repo://tests/CanDoItAll.Tests.Integration/DatabaseMigrationIntegrationTests.cs` asserts exact migration history | `bundle://proof/SB022/transcripts/postgresql-audit-migration-bootstrap-tests.txt` | `bundle://proof/SB024/transcripts/red-team-durable-audit-shallow-proof-rejection.txt` |

## Gate Result
Gate H is semantically adequate for P08. The verification host beta now has durable EF audit persistence, a bounded typed query API, redacted requester persistence, preserved observation hashes, and PostgreSQL migration/bootstrap proof rejecting in-memory-only or migration-only closure.
