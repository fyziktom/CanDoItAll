# SB11 Proof Manifest

## Status

- Subbundle: `SB11`
- Status: `Completed`
- Owned requirements: `R07`, `R08`
- Owned raw notes: project/workbench source adapters, explicit project ingestion action, provider-selected ingestion jobs, same source snapshot contract family, denied-scope behavior, missing-project behavior, provider driver boundary, and runtime composition/migration support for generic source ingestion.

## Semantic Invariant Contract

- Contract: `bundle://proof/SB11/semantic-invariants.md`

## Changed File Hashes

| File | After SHA-256 |
| --- | --- |
| `repo://src/Memory/CanDoItAll.Memory.Application/MemorySourceGatewayContracts.cs` | `1931030bdd6e216b03e9534070cae16cca0dddaf9ec1d889a991430d7a146c92` |
| `repo://src/Memory/CanDoItAll.Memory.Application/MemorySourceGateway.cs` | `67478f8b2d33231b77f7f7b4040b2ad7c9f6349aaeedd9d067ad35843bbd93ae` |
| `repo://src/Memory/CanDoItAll.Memory.Application/MemorySourceIngestionContracts.cs` | `8a776ea99870fecfa37e193556dd9caeb78e0468574ce33df3797ef1d2bb8682` |
| `repo://src/Memory/CanDoItAll.Memory.Persistence/MemoryPersistenceServiceCollectionExtensions.cs` | `559b398485aa4669aa79acc4ed93f33bac75be2a86cad556de472cdb6a896eab` |
| `repo://src/App/CanDoItAll.Composition/CanDoItAll.Composition.csproj` | `730167fd09d7b9cded500b524bdd2c5fc9051c14e90816843ed7e0267662b098` |
| `repo://src/App/CanDoItAll.Composition/ModuleAssemblies.cs` | `46dd726a2c3bcf9fe45b461bfb249c531fb677126275ea8c51d95e0ca5d32792` |
| `repo://src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs` | `7779185a1ab1c317a74ecc05a715915a59cb1a86d40c7c87d0241ec0aa70a17f` |
| `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260705163628_GenericMemoryProviderRuntime.cs` | `75735ec3c80ecdcfbab9795196fb8a759beb32741833b627016124ba7621431c` |
| `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260705163628_GenericMemoryProviderRuntime.Designer.cs` | `578eac5445461d68de18baab7e66eb8ae403380e7f313a1e5aca0ae62b14677d` |
| `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/AppDbContextModelSnapshot.cs` | `9758849636976e66877a0785b61b93632ae227f4dbcb4e75d85994c21078f60e` |
| `repo://src/Modules/CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj` | `bd830bda74293956c37676ecc1db40d065826e411d23c0ebe181db26de9973e5` |
| `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectMemoryIngestionService.cs` | `6914f2cc9353fbfebf14a705b4ab2bce189c3ab3174df29340684c644f01d351` |
| `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/WorkbenchProjectStructureMemorySourceGatewayAdapter.cs` | `9800e5f312cf2ec111d6c56ad295d9ec95a42e4ba374c1f4ab1f178b26b528df` |
| `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/WorkbenchProjectStructureSourceSnapshotProvider.cs` | `73e8e3b1f60eb4fe6c96a9db92281e16ce8684896ba2a90b0507837a909be5bf` |
| `repo://src/Modules/CanDoItAll.Modules.Workbench/Services/WorkbenchModuleServiceCollectionExtensions.cs` | `31399a3a65aea3bf335e7c20ba08366769835625afc3cccc3ec7fb2fda4fec3e` |
| `repo://tests/Memory/CanDoItAll.Memory.Tests/MemorySourceGatewayTests.cs` | `ecf515cba6de9caa06b6d60a383951bf895991fb4cafb569f8e08a64380310cf` |
| `repo://tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj` | `410f709bf83fcd41662be17029f63d6de4fd1fc08d6e7343a27314b3b43b9d1e` |
| `repo://tests/Integration/CanDoItAll.Tests.Integration/WorkbenchSourceSnapshotIntegrationTests.cs` | `f3b4ab7154c69a3c79fbfb5240fcacd388eb3ad685b66ecfdac24d7a4cec39d7` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj` | `5b91b966a2f3a5c719a77a247d47c42a0b8acfe409f5e6d6448a7e0722b094f0` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/ProjectMemoryIngestionServiceTests.cs` | `b8004f489d826a9f60a7b48e731b2a9c8e8a7074eeec45704c4d366a8c95485b` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/ProjectWorkbenchServiceArchitectureTests.cs` | `869c5f1dcf734b3adbb3c8f77742106b862ca880da4b59fa8b3bdb81db68fec0` |
| `bundle://proof/SB11/transcripts/passing-memory-test-suite.txt` | `b623292bd4fde70758891b19f7d4e5936c6eefccb4511bfd499344671459e1e0` |
| `bundle://proof/SB11/transcripts/passing-solution-build.txt` | `50065b6e54a64a5195bda0bb03a6065bc12dbca6455f2d3cf9a5d8638b89589a` |
| `bundle://proof/SB11/transcripts/passing-source-gateway-tests.txt` | `3322ec9022f21bc3decafd5a3208dbeafbb1192b32878b498556ff8ce2ab7eb1` |
| `bundle://proof/SB11/transcripts/passing-workbench-source-integration-tests.txt` | `a92d540a7aae1b7f09d52f478d7e7675da18935490d4f0c5ddb7d9e14fe90ab7` |
| `bundle://proof/SB11/transcripts/passing-workbench-source-unit-tests.txt` | `79fa3c13b7988288e8930f344da7fb5ca130d1722a2f8fdcbadb1b0c3e531e45` |
| `bundle://proof/SB11/transcripts/source-audit-anti-stub.txt` | `9dbbc1ac66bc6f23d70d7582bbbbccc94c65efb9197e85fd5c05303b1d4689f7` |
| `bundle://proof/SB11/transcripts/source-audit-memory-migration-scope.txt` | `9343e678e34b9266aa09d947f3d7d261c5f6930aa6a14cd83a5ec53594307448` |
| `bundle://proof/SB11/transcripts/source-audit-provider-driver-boundary.txt` | `5117d32340a5c0af8efd324fdcbde3dd6db38035308fecc5951ed11aae8bd01d` |
| `bundle://proof/SB11/transcripts/source-audit-source-snapshot-contract-family.txt` | `2e5dfc9d643ef4310d1e37b052e520dbb4a7cf2b8f459ff29880e81169843887` |
| `bundle://evidence/16-prepared-stage-validation-after-sb11.txt` | `e73182321d0dda1c9eba13fc7a971309c2620250430a99355b85dc72e7838ab5` |

## Command Transcripts

| Purpose | Transcript |
| --- | --- |
| Focused generic source gateway tests | `bundle://proof/SB11/transcripts/passing-source-gateway-tests.txt` |
| Workbench adapter/manual ingestion unit tests | `bundle://proof/SB11/transcripts/passing-workbench-source-unit-tests.txt` |
| Workbench source adapter integration tests | `bundle://proof/SB11/transcripts/passing-workbench-source-integration-tests.txt` |
| Full generic memory test suite | `bundle://proof/SB11/transcripts/passing-memory-test-suite.txt` |
| Solution build | `bundle://proof/SB11/transcripts/passing-solution-build.txt` |
| Provider driver boundary audit | `bundle://proof/SB11/transcripts/source-audit-provider-driver-boundary.txt` |
| Source snapshot contract family audit | `bundle://proof/SB11/transcripts/source-audit-source-snapshot-contract-family.txt` |
| Anti-stub audit | `bundle://proof/SB11/transcripts/source-audit-anti-stub.txt` |
| Generic memory migration scope audit | `bundle://proof/SB11/transcripts/source-audit-memory-migration-scope.txt` |

## Passing Proof

- Generic gateway transcript: exit code `0`, seven `MemorySourceGatewayTests` passed, including `SB11_SG001_Denied_requested_scope_fails_before_adapter_call`.
- Workbench unit transcript: exit code `0`, four focused tests passed across `ProjectMemoryIngestionServiceTests` and `ProjectWorkbenchServiceArchitectureTests`.
- Workbench integration transcript: exit code `0`, four `WorkbenchSourceSnapshotIntegrationTests` passed for project snapshot, denied scope, missing project, and existing redaction/cursor behavior.
- Full memory transcript: exit code `0`, all 61 generic memory tests passed.
- Solution build transcript: exit code `0`, with known NU1900 vulnerability-index fetch warnings and NU1903 `Microsoft.OpenApi` advisory warnings only.
- Bundle validation transcript: `bundle://evidence/16-prepared-stage-validation-after-sb11.txt`, exit code `0`.

## Source Assertions

- Provider driver boundary audit proves `src/Memory/CanDoItAll.Memory.Http` and `src/Memory/CanDoItAll.Memory.Mcp` contain no Workbench/project adapter references.
- Source snapshot contract family audit proves `MemorySourceSnapshot`, `ProjectStructureSourceSnapshotRequest`, and `IProjectStructureSourceSnapshotProvider` remain canonical in MAF Core; Workbench only wraps/uses them.
- Memory migration scope audit proves runtime composition registration is backed by a PostgreSQL migration scoped to `Memory_*` generic runtime tables and indexes.
- Anti-stub audit found no SB11-relevant TODO, `NotImplementedException`, placeholder, fixture-specific, stubbed, or fake-only markers.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Scope-aware source gateway policy | `repo://src/Memory/CanDoItAll.Memory.Application/MemorySourceGatewayContracts.cs` and `MemorySourceGateway.cs` | `SB11_SG001` and integration denied-scope test | request source kind and requested scope are checked before adapter dispatch | denied scope returns `DeniedSourceScope` and adapter is not called |
| Workbench source gateway adapter | `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/WorkbenchProjectStructureMemorySourceGatewayAdapter.cs` | Workbench integration tests | wraps existing `IProjectStructureSourceSnapshotProvider` and returns MAF snapshots | source kind/scope mismatch throws explicit adapter error |
| Manual project ingestion action service | `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectMemoryIngestionService.cs` | unit tests | captures source snapshot through gateway before enqueueing provider job | denied gateway result throws and does not enqueue |
| Missing-project source semantics | `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/WorkbenchProjectStructureSourceSnapshotProvider.cs` | integration missing-project test | returns valid empty `EndOfSource` snapshot for deleted/missing project | empty GUID still fails input validation |
| Runtime composition and migration | `repo://src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs` and migration files | integration DI/bootstrap tests | generic memory module is registered before Workbench and backed by migration snapshot | pending-model-change failure was removed by migration, not suppressed |

## Browser Validation

- Browser validation: `N/A`.
- Reason: SB11 adds non-UI source adapters, manual ingestion service entry point, runtime composition, and tests. No browser-visible route or component behavior was changed.

## Closure Decision

- SB11 closure gate: `Pass`.
- Reopened subbundles: `None`.
- Host validation: runtime composition now registers generic memory persistence so Workbench ingestion dependencies validate.
- Downstream permission: SB12 process/workflow/agent source adapters may start after bundle-level validation passes.
