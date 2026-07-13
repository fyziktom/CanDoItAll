# SB13 Semantic Invariants

## Invariant SB13_CRM001

- Invariant ID: `SB13_CRM001`
- Source raw note: CRM/HR and client/account records must be available through Source Gateway adapters without leaking sensitive customer or HR payloads.
- Expected behavior: `CrmHrSourceSnapshotProvider` returns MAF `MemorySourceSnapshot` items for parties, account profiles, opportunities, interactions, and workforce profiles, with sensitive notes/contact values redacted before provider delivery.
- Disallowed shallow implementation: emitting ids/counts only, passing EF entities or DbContext through the memory boundary, or letting private CRM notes/contact points leave the module unredacted.
- Failing-first proof: scoped-service failure was captured in `bundle://proof/SB13/transcripts/failing-first-crm-resource-source-unit-tests.txt`; no earlier pre-implementation transcript existed after resume.
- Passing proof: `Crm_hr_source_adapter_exposes_party_account_opportunity_interaction_and_workforce_with_sensitive_redaction` in `bundle://proof/SB13/transcripts/passing-crm-resource-source-unit-tests.txt`.
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrSourceSnapshotProvider.cs`, `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrMemorySourceGatewayAdapter.cs`, `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrModuleServiceCollectionExtensions.cs`, and `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Sources/MemorySourceSnapshotContracts.cs`.
- Production assertions: `bundle://proof/SB13/transcripts/source-audit-source-snapshot-contract-family.txt` and `bundle://proof/SB13/transcripts/source-audit-provider-driver-boundary.txt`.
- Red-team negative case: unit test seeds `crm-secret` and a private email and asserts neither appears in provider-visible content.
- Downstream dependency check: SB14 and SB15-SB18 can rely on CRM/HR source snapshots using the generic gateway and MAF snapshot family.

## Invariant SB13_RES001

- Invariant ID: `SB13_RES001`
- Source raw note: resource metadata snapshots must expose references without leaking config JSON, linked secret ids, or sensitive URL query parameters.
- Expected behavior: `ResourceSourceSnapshotProvider` returns resource reference items with safe metadata and storage references, redacting sensitive locators and omitting config/secret fields.
- Disallowed shallow implementation: copying resource config JSON into snapshot content, exposing linked secret identifiers, or preserving credential query parameters in provider-visible locators.
- Failing-first proof: resource adapter availability failure was captured with the CRM/resource focused transcript in `bundle://proof/SB13/transcripts/failing-first-crm-resource-source-unit-tests.txt`; no earlier pre-implementation transcript existed after resume.
- Passing proof: `Resource_source_adapter_exposes_metadata_and_references_without_secret_values` in `bundle://proof/SB13/transcripts/passing-crm-resource-source-unit-tests.txt`.
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Resources/ResourceSourceSnapshotProvider.cs`, `repo://src/Modules/CanDoItAll.Modules.Resources/ResourceMemorySourceGatewayAdapter.cs`, `repo://src/Modules/CanDoItAll.Modules.Resources/ResourcesModuleServiceCollectionExtensions.cs`, and `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Sources/MemorySourceSnapshotContracts.cs`.
- Production assertions: `bundle://proof/SB13/transcripts/source-audit-source-snapshot-contract-family.txt` and `bundle://proof/SB13/transcripts/source-audit-provider-driver-boundary.txt`.
- Red-team negative case: unit test seeds `resource-secret`, `config-secret`, and a linked secret id and asserts they are absent from content and locator.
- Downstream dependency check: SB14 can harden source gateway behavior with resource catalog adapters included.

## Invariant SB13_MAN001

- Invariant ID: `SB13_MAN001`
- Source raw note: manually supplied text, files, and links must be ingested through the generic source gateway using the same source snapshot contract family.
- Expected behavior: manual text creates text snapshot content after safety checks; manual file/link inputs produce references/storage locators without copying file bytes; unsafe text/links fail before ledger enqueue.
- Disallowed shallow implementation: bypassing Source Gateway, copying upload bytes into source snapshots, or accepting credential-shaped text and sensitive URL query parameters.
- Failing-first proof: `bundle://proof/SB13/transcripts/failing-first-manual-source-ingestion-tests.txt` captured the manual ingestion path before source job snapshot identity round-tripped correctly.
- Passing proof: `Manual_text_ingestion_captures_snapshot_source_job_and_operation_identity`, `Manual_file_and_link_sources_expose_references_without_copying_payload_bytes`, and `Manual_link_with_sensitive_query_is_rejected_before_ledger_enqueue` in `bundle://proof/SB13/transcripts/passing-manual-source-ingestion-tests.txt`.
- Changed source files: `repo://src/Memory/CanDoItAll.Memory.Application/ManualMemorySourceContracts.cs`, `repo://src/Memory/CanDoItAll.Memory.Application/ManualMemorySourceSafetyPolicy.cs`, `repo://src/Memory/CanDoItAll.Memory.Application/ManualMemorySourceGatewayAdapter.cs`, `repo://src/Memory/CanDoItAll.Memory.Application/ManualSourceSnapshotRequestFactory.cs`, and `repo://src/Memory/CanDoItAll.Memory.Application/MemorySourcePayloadClassifier.cs`.
- Production assertions: `bundle://proof/SB13/transcripts/source-audit-source-snapshot-contract-family.txt`.
- Red-team negative case: sensitive URL query parameter rejection throws before the source ledger receives a job.
- Downstream dependency check: SB20-SB22 can build manual ingestion UI on the generic service without inventing a second source path.

## Invariant SB13_ING001

- Invariant ID: `SB13_ING001`
- Source raw note: manual ingestion must record provider id, source snapshot id, and operation id in the same operation/source ledger path as provider-requested ingestion.
- Expected behavior: `ManualMemorySourceIngestionService` captures the source snapshot through `IMemorySourceGateway`, creates a memory operation with `MemoryCapabilityIds.IngestionSnapshot`, and enqueues a source job with provider id, captured snapshot id, and operation id.
- Disallowed shallow implementation: enqueueing a manual job without operation ledger correlation, manually seeding a source snapshot id in tests, or returning an operation id that is not persisted.
- Failing-first proof: `bundle://proof/SB13/transcripts/failing-first-manual-source-ingestion-tests.txt` captured `CapturedSnapshotId` round-tripping as `null` before the MAF snapshot id JSON constructor fix.
- Passing proof: `Manual_text_ingestion_captures_snapshot_source_job_and_operation_identity` in `bundle://proof/SB13/transcripts/passing-manual-source-ingestion-tests.txt`.
- Changed source files: `repo://src/Memory/CanDoItAll.Memory.Application/ManualMemorySourceIngestionService.cs`, `repo://src/Memory/CanDoItAll.Memory.Application/MemorySourceIngestionContracts.cs`, `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Sources/MemorySourceSnapshotContracts.cs`, and `repo://src/Memory/CanDoItAll.Memory.Persistence/MemoryPersistenceServiceCollectionExtensions.cs`.
- Production assertions: `bundle://proof/SB13/transcripts/source-audit-manual-ingestion-ledger.txt`.
- Red-team negative case: unsafe link rejection proves the ledger remains empty when source capture fails safety checks.
- Downstream dependency check: SB15-SB18 can consume manual ingestion jobs through operation/source ledger correlation.

## Invariant SB13_FUT001

- Invariant ID: `SB13_FUT001`
- Source raw note: future source modules must register adapters without editing generic provider drivers or bypassing source gateway policy.
- Expected behavior: `AddMemorySourceGatewayAdapter<TAdapter>()` registers scoped source adapters; the generic source gateway still enforces requested source kind and scope before dispatch.
- Disallowed shallow implementation: hard-coding every source adapter into HTTP/MCP provider drivers, registering adapters outside DI, or allowing adapter dispatch before source gateway policy checks.
- Failing-first proof: no earlier pre-implementation transcript existed after resume; denied-scope red-team proof is captured in the passing CRM/resource unit transcript.
- Passing proof: `Future_source_adapter_registration_still_enforces_gateway_policy_before_dispatch` and `Crm_hr_and_resources_modules_register_source_gateway_adapters` in `bundle://proof/SB13/transcripts/passing-crm-resource-source-unit-tests.txt`.
- Changed source files: `repo://src/Memory/CanDoItAll.Memory.Application/MemorySourceGatewayServiceCollectionExtensions.cs`, `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrModuleServiceCollectionExtensions.cs`, and `repo://src/Modules/CanDoItAll.Modules.Resources/ResourcesModuleServiceCollectionExtensions.cs`.
- Production assertions: `bundle://proof/SB13/transcripts/source-audit-provider-driver-boundary.txt`.
- Red-team negative case: fake future adapter increments a read counter only if dispatched; denied manual scope returns `DeniedSourceScope` and read count remains zero.
- Downstream dependency check: SB14 can checkpoint generic source gateway hardening with future registration included.

## Raw Note Closure

| Raw note | Status | Proof |
| --- | --- | --- |
| CRM/HR/client/account source adapters with sensitive redaction | Solved | `SB13_CRM001` and `bundle://proof/SB13/transcripts/passing-crm-resource-source-unit-tests.txt` |
| Resource metadata snapshots without secret/config leakage | Solved | `SB13_RES001` and `bundle://proof/SB13/transcripts/passing-crm-resource-source-unit-tests.txt` |
| Manual text/file/link ingestion through Source Gateway | Solved | `SB13_MAN001` and `bundle://proof/SB13/transcripts/passing-manual-source-ingestion-tests.txt` |
| Manual ingestion ledger records provider id, source snapshot id, and operation id | Solved | `SB13_ING001`, failing-first manual transcript, and passing manual transcript |
| Future source adapter registration without provider-driver edits | Solved | `SB13_FUT001`, provider-driver boundary audit, and future adapter denied-scope unit test |

## Shallow-Pass Trap

- A DTO-only source adapter would compile but fork source snapshots; the contract-family audit proves reuse of MAF `MemorySourceSnapshot`.
- A redaction policy that only hides one fixture key would leak private contact or linked secret values; CRM/resource tests seed several sensitive fields and assert absence in provider-visible content.
- A manual ingestion service that only returns an operation id would look successful while leaving source ledger correlation blank; the failing-first and passing manual transcripts prove the ledger round trip.
- A future registration API that only adds adapters to DI would still be unsafe if policy dispatch happened too late; the denied-scope test proves zero adapter reads.
- A large-file implementation would pass focused adapter tests while violating foundation maintainability; the failing-first memory suite and passing memory suite prove the file split.

## Downstream Dependency Check

- SB14 can checkpoint source gateway hardening with CRM/HR, resources, manual sources, Workbench, process, workflow, and future adapters included.
- SB15-SB18 can route memory operations and MAF tool/executor source requests through the generic source gateway without direct provider-driver reads.
- SB20-SB22 can build UI around manual ingestion and source jobs because provider id, snapshot id, and operation id are persisted together.
- SB30 can continue base-host dependency removal because generic HTTP/MCP drivers remain free of module-specific source adapter references.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| CRM/HR source snapshot provider | `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrSourceSnapshotProvider.cs` | CRM/HR focused unit proof in `bundle://proof/SB13/transcripts/passing-crm-resource-source-unit-tests.txt` | `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrModuleServiceCollectionExtensions.cs` registers provider and adapter | seeded `crm-secret` and private contact values are absent from content |
| Resource source snapshot provider | `repo://src/Modules/CanDoItAll.Modules.Resources/ResourceSourceSnapshotProvider.cs` | resource focused unit proof in `bundle://proof/SB13/transcripts/passing-crm-resource-source-unit-tests.txt` | `repo://src/Modules/CanDoItAll.Modules.Resources/ResourcesModuleServiceCollectionExtensions.cs` registers provider and adapter | seeded token/config/linked secret values are absent from content and locator |
| Manual source snapshot provider | `repo://src/Memory/CanDoItAll.Memory.Application/ManualMemorySourceGatewayAdapter.cs` | manual text/file/link proof in `bundle://proof/SB13/transcripts/passing-manual-source-ingestion-tests.txt` | `repo://src/Memory/CanDoItAll.Memory.Persistence/MemoryPersistenceServiceCollectionExtensions.cs` registers manual provider, adapter, and service | sensitive query rejection prevents source ledger enqueue |
| Manual source ingestion job identity | `repo://src/Memory/CanDoItAll.Memory.Application/ManualMemorySourceIngestionService.cs` | source and operation ledger assertions in `bundle://proof/SB13/transcripts/passing-manual-source-ingestion-tests.txt` | source request ledger and operation ledger stores are used by production service | failing-first transcript proves missing snapshot id was caught before closure |
| Future source adapter registration | `repo://src/Memory/CanDoItAll.Memory.Application/MemorySourceGatewayServiceCollectionExtensions.cs` | future adapter denied-scope unit proof in `bundle://proof/SB13/transcripts/passing-crm-resource-source-unit-tests.txt` | module service registration uses scoped `IMemorySourceGatewayAdapter` entries | gateway returns `DeniedSourceScope` and adapter `ReadCount` stays zero |
| Source snapshot contract family | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Sources/MemorySourceSnapshotContracts.cs` | `bundle://proof/SB13/transcripts/source-audit-source-snapshot-contract-family.txt` | all adapters emit MAF snapshot records for downstream ingestion | provider-driver boundary audit proves generic drivers do not own module-specific source reads |
