# SB13 Proof Manifest

## Status

- Subbundle: `SB13`
- Status: `Completed`
- Owned requirements: `R07`, `R08`
- Owned raw notes: CRM/HR, resource catalog, manual text/file/link source adapters; sensitive customer/resource redaction; future source registration without generic driver edits; denied source scopes; manual ingestion ledger identity.

## Semantic Invariant Contract

- Contract: `bundle://proof/SB13/semantic-invariants.md`

## Changed File Hashes

| File | After SHA-256 |
| --- | --- |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Sources/MemorySourceSnapshotContracts.cs` | `f1fa55c6e6a3d2f15a41de62522afdbcc0b2386f45f5b5aa220b9ac6b4a4af0c` |
| `repo://src/Memory/CanDoItAll.Memory.Application/CanDoItAll.Memory.Application.csproj` | `8e2e3c13e1f9ac822a14ca7c29c106ad040c6e1386728654780bfca3556ce989` |
| `repo://src/Memory/CanDoItAll.Memory.Application/MemorySourceGatewayContracts.cs` | `1bf4b8a96f808fc29e31c54d75e23555750d59f34772a2a71c6ed02ff7a9e1f4` |
| `repo://src/Memory/CanDoItAll.Memory.Application/MemorySourcePayloadClassifier.cs` | `fe917fe28eaa76adc8bbdd926ecc20fb5786d7c14c2a030e04becbb6ae023609` |
| `repo://src/Memory/CanDoItAll.Memory.Application/MemorySourceIngestionContracts.cs` | `fecc3dae7d14c6b390bdf9a0680208adfb8d86214186827de9294335ab098ab3` |
| `repo://src/Memory/CanDoItAll.Memory.Application/MemorySourceGatewayServiceCollectionExtensions.cs` | `31b7b38a56a85409b23c8191263c28347081f32da8a07efb7505e05f3208a46b` |
| `repo://src/Memory/CanDoItAll.Memory.Application/ManualMemorySourceContracts.cs` | `f33a9ea21c8518b5bba4bdda9aefe867d655ba08a11211f96f3ad1e24dfd6420` |
| `repo://src/Memory/CanDoItAll.Memory.Application/ManualMemorySourceSafetyPolicy.cs` | `7565709e13904a9c97d3b8a1af242bb62b4b1d2cb10d6c1a6e0df711a0fa4301` |
| `repo://src/Memory/CanDoItAll.Memory.Application/ManualMemorySourceGatewayAdapter.cs` | `7bea98489cf0c0546c2f7ccb8aa452ceeb412ee0696d88a663f93a3b4dc62feb` |
| `repo://src/Memory/CanDoItAll.Memory.Application/ManualSourceSnapshotRequestFactory.cs` | `bbfe3e75fb399c4aaab09cf3a5ec30763d15072bd895d3e0b40d1faf8963606a` |
| `repo://src/Memory/CanDoItAll.Memory.Application/ManualMemorySourceIngestionService.cs` | `aa4d5987d69bacf15002a2afa2e2dc1fa16a64a9ad07da16d591ed7349468c9f` |
| `repo://src/Memory/CanDoItAll.Memory.Persistence/MemoryPersistenceServiceCollectionExtensions.cs` | `763150d27a0ba5b6c3ddaca54a3fa966b507ad6dce157587000039c7a0e7988a` |
| `repo://src/Modules/CanDoItAll.Modules.CrmHr/CanDoItAll.Modules.CrmHr.csproj` | `6c505da7b8cbac1d1074860361309ab8ea3ed1928c2903f43e7dc864330a9838` |
| `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrModuleServiceCollectionExtensions.cs` | `60c9b6ce4aa27a4e36de1ac2441f10ff666438f4e9f62159b74301fde47a60e8` |
| `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrMemorySourceGatewayAdapter.cs` | `3281511c6830e475648985e173c448f7c28840534511be868a98cff80070ba1f` |
| `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrSourceSnapshotProvider.cs` | `4a4ed746baa1dd25d855adae821dba4668365d9201c16f4adb90a09e62bfdda4` |
| `repo://src/Modules/CanDoItAll.Modules.Resources/CanDoItAll.Modules.Resources.csproj` | `f77e9f2ffc94be165e7625f1871d2d295088a357624aa7c27eed3f12cef8686d` |
| `repo://src/Modules/CanDoItAll.Modules.Resources/ResourcesModuleServiceCollectionExtensions.cs` | `e5beb39063d2ce53e2f37ccc343f0bafad10a5426ad8bfefceb916257d907d44` |
| `repo://src/Modules/CanDoItAll.Modules.Resources/ResourceMemorySourceGatewayAdapter.cs` | `ddcd6874dab9936ba97fed2ddd75271a6bd4c4cf040cb0c7ee72075d78c3fd0d` |
| `repo://src/Modules/CanDoItAll.Modules.Resources/ResourceSourceSnapshotProvider.cs` | `64c3d574fedfb304f013e0361f9ecfa13b232e95e57e252540311ec81c94a7da` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj` | `2b91518ab3addee94a787b0c5f3fe4f11f2d9d1fe67eaa064c6ba08077c41f18` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/CrmHrResourceSourceGatewayAdapterTests.cs` | `329638fa73f756c456e817c44b588fb5558d0dd5f73207f0c59d9fb497b5366a` |
| `repo://tests/Memory/CanDoItAll.Memory.Tests/ManualMemorySourceIngestionTests.cs` | `57de532624a361ecd42d8b60c1445d33dcf1e55bd782161bd9895ed1693316ad` |
| `bundle://proof/SB13/transcripts/failing-first-crm-resource-source-unit-tests.txt` | `308609abf8b95f4ce263a4dedf051aca06977478a8ee46f2ff089c6cf60cfcbe` |
| `bundle://proof/SB13/transcripts/failing-first-manual-source-ingestion-tests.txt` | `5fcc37a240897677d24f5aba333dda205304cd8d1e3d948c03916c03e276b48b` |
| `bundle://proof/SB13/transcripts/failing-first-memory-test-suite.txt` | `5dd865593043c30f6d7a3d5c59057e0a67b7f8d46fc0f4629f60ac8d379eb132` |
| `bundle://proof/SB13/transcripts/passing-crm-resource-source-unit-tests.txt` | `8a42b57ddf1782b52f0c99d411fc3217b3feaa727621b2f6f71bee295f6f463a` |
| `bundle://proof/SB13/transcripts/passing-manual-source-ingestion-tests.txt` | `c6cf9f9ae5525017634e270af748a533051e3b51ee53177dd0f2258d46d7a567` |
| `bundle://proof/SB13/transcripts/passing-memory-test-suite.txt` | `07064040a4fb0f53ea11055b90f44b3683274f87e3b1000a1bff153ad4a77859` |
| `bundle://proof/SB13/transcripts/passing-solution-build.txt` | `9bf9cbc487d59fd279047c1905fcd155c1759e7f02101b0e7e9f80b12cde8dc2` |
| `bundle://proof/SB13/transcripts/passing-source-adapter-regression-tests.txt` | `a8d3f598f1d48d669fb68875b2c89025d63a5fd938c227d14aa867a3f0997365` |
| `bundle://proof/SB13/transcripts/passing-workbench-source-integration-regression-tests.txt` | `4589aecb54dfe7e844502c6866891ca947911d5e5ecc5d8f17825259ad545fd8` |
| `bundle://proof/SB13/transcripts/source-audit-anti-stub.txt` | `ebd825d5533e5ecc878c49055267f6382b1415254d752cfeb9587d35b958a773` |
| `bundle://proof/SB13/transcripts/source-audit-manual-ingestion-ledger.txt` | `9c2c47b25ed68ee81b5b6f5bcdf60b6c872896da6e3cd065218aefc8cdf2d206` |
| `bundle://proof/SB13/transcripts/source-audit-provider-driver-boundary.txt` | `8e8d3bdd2424b148af515804e35c0fbf594d3b8864704801f5716c73fde7c4d6` |
| `bundle://proof/SB13/transcripts/source-audit-semantic-invariant-ids.txt` | `8fb6f165dbbcedd55a3c4c702c0dc81feea30f9a7d161886683c4bcfd63c8565` |
| `bundle://proof/SB13/transcripts/source-audit-source-snapshot-contract-family.txt` | `937a9ef60ee8ad52ecdf36f490753c6e43160f52514ac3d356a4e126665104cb` |
| `bundle://evidence/18-prepared-stage-validation-after-sb13.txt` | `acad5f14422f3da34cd98ea169021f8c659ad0c9ff83d99785954f959914957f` |

## Command Transcripts

| Purpose | Transcript |
| --- | --- |
| Failing-first CRM/resource unit proof captured during scoped-service fix | `bundle://proof/SB13/transcripts/failing-first-crm-resource-source-unit-tests.txt` |
| Failing-first manual ledger identity proof captured before MAF snapshot id JSON fix | `bundle://proof/SB13/transcripts/failing-first-manual-source-ingestion-tests.txt` |
| Failing-first bounded-file checkpoint proof captured before manual factory split | `bundle://proof/SB13/transcripts/failing-first-memory-test-suite.txt` |
| Focused CRM/resource/future-adapter unit tests | `bundle://proof/SB13/transcripts/passing-crm-resource-source-unit-tests.txt` |
| Focused manual text/file/link ingestion tests | `bundle://proof/SB13/transcripts/passing-manual-source-ingestion-tests.txt` |
| Full generic memory test suite | `bundle://proof/SB13/transcripts/passing-memory-test-suite.txt` |
| Source adapter regression tests across SB11-SB13 | `bundle://proof/SB13/transcripts/passing-source-adapter-regression-tests.txt` |
| Workbench source integration downstream smoke | `bundle://proof/SB13/transcripts/passing-workbench-source-integration-regression-tests.txt` |
| Solution build | `bundle://proof/SB13/transcripts/passing-solution-build.txt` |
| Provider driver boundary audit | `bundle://proof/SB13/transcripts/source-audit-provider-driver-boundary.txt` |
| Source snapshot contract family audit | `bundle://proof/SB13/transcripts/source-audit-source-snapshot-contract-family.txt` |
| Manual ingestion ledger source audit | `bundle://proof/SB13/transcripts/source-audit-manual-ingestion-ledger.txt` |
| Semantic invariant id audit | `bundle://proof/SB13/transcripts/source-audit-semantic-invariant-ids.txt` |
| Anti-stub audit | `bundle://proof/SB13/transcripts/source-audit-anti-stub.txt` |
| Bundle prepared-stage validation after SB13 | `bundle://evidence/18-prepared-stage-validation-after-sb13.txt` |

## Passing Proof

- CRM/resource/future-source unit transcript: exit code `0`, four `CrmHrResourceSourceGatewayAdapterTests` passed, covering CRM/HR redaction, resource reference redaction, denied future-adapter scope before dispatch, and module registration.
- Manual ingestion transcript: exit code `0`, three `ManualMemorySourceIngestionTests` passed, covering manual text ingestion, file/link reference ingestion without payload-byte copying, and sensitive-link rejection before ledger enqueue.
- Full memory transcript: exit code `0`, all 64 generic memory tests passed after adding manual ingestion and bounded-file split.
- Source adapter regression transcript: exit code `0`, 12 source-adapter tests across SB11-SB13 passed.
- Workbench integration transcript: exit code `0`, four `WorkbenchSourceSnapshotIntegrationTests` passed to prove SB13 did not regress the SB11 source gateway path.
- Solution build transcript: exit code `0`, with known NU1900 vulnerability-index fetch warnings and NU1903 `Microsoft.OpenApi` advisory warnings only.
- Bundle validation transcript: `bundle://evidence/18-prepared-stage-validation-after-sb13.txt`, exit code `0`.

## Source Assertions

- CRM/HR source provider lives in `CanDoItAll.Modules.CrmHr`, reads CRM/HR EF data behind the module boundary, and emits MAF `MemorySourceSnapshot` items with sensitive notes/contact values redacted before provider delivery.
- Resource source provider lives in `CanDoItAll.Modules.Resources`, emits metadata and storage references, and omits config JSON and linked secret ids from provider-visible content.
- Manual source adapter and snapshot provider live in generic memory application/persistence code because manual text/file/link ingestion is a generic memory action, not a provider-driver concern.
- `MemorySourceGatewayServiceCollectionExtensions.AddMemorySourceGatewayAdapter<TAdapter>()` is the future-module registration API; the denied future adapter unit test proves registered adapters still run behind source gateway policy.
- Generic HTTP/MCP memory drivers contain no CRM, resource, or manual source adapter references.
- Source snapshot contract audit proves CRM/resource/manual adapters use the MAF `MemorySourceSnapshot` family instead of introducing a second DTO family.
- Anti-stub audit found no SB13-relevant `TODO`, `NotImplementedException`, placeholder, fixture-only, or fake-only markers in the production adapter/provider surface.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| CRM/HR source snapshot provider | `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrSourceSnapshotProvider.cs` | `Crm_hr_source_adapter_exposes_party_account_opportunity_interaction_and_workforce_with_sensitive_redaction` in `bundle://proof/SB13/transcripts/passing-crm-resource-source-unit-tests.txt` | module DI registration in `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrModuleServiceCollectionExtensions.cs` | unit test asserts `crm-secret` and private contact values do not leave the module boundary |
| Resource source snapshot provider | `repo://src/Modules/CanDoItAll.Modules.Resources/ResourceSourceSnapshotProvider.cs` | `Resource_source_adapter_exposes_metadata_and_references_without_secret_values` in `bundle://proof/SB13/transcripts/passing-crm-resource-source-unit-tests.txt` | module DI registration in `repo://src/Modules/CanDoItAll.Modules.Resources/ResourcesModuleServiceCollectionExtensions.cs` | unit test asserts resource token, config secret, and linked secret id are absent from provider-visible content/locator |
| Manual source snapshot provider | `repo://src/Memory/CanDoItAll.Memory.Application/ManualMemorySourceGatewayAdapter.cs` | manual text/file/link tests in `bundle://proof/SB13/transcripts/passing-manual-source-ingestion-tests.txt` | generic memory persistence registration in `repo://src/Memory/CanDoItAll.Memory.Persistence/MemoryPersistenceServiceCollectionExtensions.cs` | sensitive-query test rejects unsafe link before any source ledger enqueue |
| Manual source ingestion job identity | `repo://src/Memory/CanDoItAll.Memory.Application/ManualMemorySourceIngestionService.cs` | `Manual_text_ingestion_captures_snapshot_source_job_and_operation_identity` in `bundle://proof/SB13/transcripts/passing-manual-source-ingestion-tests.txt` | source and operation ledgers store provider id, captured snapshot id, and operation id | failing-first transcript shows captured snapshot id was missing before the JSON constructor fix |
| Future source adapter registration | `repo://src/Memory/CanDoItAll.Memory.Application/MemorySourceGatewayServiceCollectionExtensions.cs` | `Future_source_adapter_registration_still_enforces_gateway_policy_before_dispatch` in `bundle://proof/SB13/transcripts/passing-crm-resource-source-unit-tests.txt` | modules can add scoped `IMemorySourceGatewayAdapter` registrations without provider-driver edits | denied-scope test proves gateway policy rejects before adapter `ReadSnapshotAsync` dispatch |
| Source snapshot contract family | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Sources/MemorySourceSnapshotContracts.cs` | `bundle://proof/SB13/transcripts/source-audit-source-snapshot-contract-family.txt` | CRM/resource/manual/future adapters share MAF snapshot contracts for downstream ingestion | no second source snapshot DTO family is introduced |

## Browser Validation

- Browser validation: `N/A`.
- Reason: SB13 adds non-UI source gateway adapters, manual ingestion application services, and module registration. No browser-visible route or component behavior changed.

## Closure Decision

- SB13 closure gate: `Pass`.
- Reopened subbundles: `None`.
- Host validation: solution build and downstream Workbench integration smoke passed.
- Downstream permission: SB14 ingestion source gateway hardening checkpoint may start after bundle-level validation passes.
