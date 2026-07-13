# SB02 Behavioral Proof

Date: 2026-07-12.

## Raw Notes Owned

- `N002`: improve Storage before UI.
- `N003-N004`: establish architecture and meaningful tests before dependent implementation.
- `N008`: make listing cache policy explicit and Disabled by default.
- `N015`: large file sources require bounded work, state, concurrency, and time rather than page-size-only proof.

## Shipped Behavior

- Infrastructure owns a separate `IStorageBrowseDriver` sidecar; existing `IStorageDriver` remains unchanged.
- Browse, optional search/stat facets, capabilities, ordering, cursor, consistency, completeness, errors, entries, metrics, and work/search/retention budgets are typed and bounded.
- `StorageBrowseDriverRegistry` rejects duplicate providers, missing providers, unsupported operations, and capability/interface mismatches. It never selects a default provider or last registration.
- `StorageProviderConfiguration.BrowseCache` defaults missing legacy JSON to Disabled, validates enabled Memory policies, and explicitly rejects Hybrid until durable shared revision exists.
- Configuration parsing, serialization, and catalog save validate the cache policy before persistence.
- Infrastructure DI registers the native browse registry without any FileTools package or project reference.

## Source Proof

- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/Browse/StorageBrowsePrimitives.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/Browse/StorageBrowseBudgets.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/Browse/StorageBrowseModels.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/Browse/StorageBrowseDrivers.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/Browse/StorageBrowseSettings.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/Models/StorageModels.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/StorageJson.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/Persistence/StorageCatalogService.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`

No `.csproj` changed. Source audit found no `CanDoItAll.FileTools`, service location, new partial class, `TODO`, `FIXME`, or `NotImplemented` path in the new contract or tests.

## Test And Build Proof

- `dotnet build .\src\Foundation\CanDoItAll.Infrastructure\CanDoItAll.Infrastructure.csproj -c Release --no-restore -warnaserror`; Pass, 0 warnings/errors.
- Unit tests filtered by `FullyQualifiedName~Storage`; Pass, 47 tests.
- Integration tests filtered by `FullyQualifiedName~Storage`; Pass, 10 tests.
- Focused `dotnet format .\CanDoItAll.slnx --verify-no-changes --no-restore --include ...`; Pass.
- Prepared bundle validator after durable updates; Pass.

Behavioral positives:

- `DistinctProviderShapes_ExecuteThroughNativeContract` proves a minimal filesystem-shaped provider and a searchable/stat-capable IPFS-shaped provider execute through the same native contract without fake optional members.
- `ProviderConfiguration_MemoryCache_RoundTripsTypedSettings` proves a realistic bounded Memory policy round-trips through current `ConfigJson` serialization.

Adversarial negatives:

- duplicate registration, unknown provider, unsupported search, and advertised-search-without-facet all fail with typed codes;
- page size exceeding its returned-item budget, malformed cursor, inconsistent page metrics, Hybrid mode, and enabled/Disabled mismatch fail predictably;
- invalid catalog cache configuration is rejected before persistence.

## Shallow-Pass Trap

A shallow implementation could return 50 rows after unbounded work, use last-registration-wins, advertise search without implementing it, or deserialize missing/invalid cache settings into an enabled fallback. The structural budget records plus the named negative tests reject each shortcut. Provider implementations must still prove actual counters in SB03/SB04.

## Performance Scan

Pass 1: this phase defines cold construction/validation contracts; it does not perform provider I/O. The only per-result materialization is a defensive copy of already bounded path/entry collections. The registry dictionary and sorted key array are built once at DI construction.

Pass 2 exact scan counts on the new browse contracts plus `StorageJson`:

| Recipe | Hits | Decision |
| --- | ---: | --- |
| literal `IndexOf` / `Substring` / literal `StartsWith`-`EndsWith` / literal `Contains` | 0 / 0 / 0 / 0 | no string comparison/allocation candidate |
| parameterless `ToLower`/`ToUpper`, chained Replace, `params`, char LINQ | 0 / 0 / 0 / 0 | no candidate |
| static Dictionary / FrozenDictionary | 0 / 0 | no static lookup table |
| per-call List / Dictionary | 0 / 1 | one registry dictionary at singleton construction; bounded by registered providers |
| LINQ hot-path chains | 0 | none |
| `async void`, task `.Result`, `.Wait`, `Task.Run` | 0 / 0 / 0 / 0 | async boundary remains non-blocking |
| `new HttpClient` | 0 | no transport in this phase |
| `new JsonSerializerOptions` | 1 | existing `StorageJson` static cached singleton, positive pattern |
| sealed / unsealed new declarations | 19 / 0 | all new concrete records/classes sealed |

No performance finding required a code change. Provider structural work remains intentionally owned by SB03/SB04.

## Architecture Proof

- Before cleanup snapshot: `snap-20260713015249-91e8d499` identified one actionable 674-line contract-file finding.
- Cleanup split primitives, budgets, models, drivers, and settings into focused files of 88-288 lines without changing the public behavior.
- After snapshot: `snap-20260713015817-7f2dc30d`, one project, 228 types, 1,274 members, no blocking diagnostics. The large-file finding is gone.
- No project reference changed and no project cycle exists. The known Infrastructure Persistence/ControlPlane module cycle remains unchanged.

## Anti-Stub Audit And Progression

No template-only implementation or placeholder production path remains. DI registers the registry, config load/save uses validation, and direct fake providers execute the contract. SB02 closure gate: `Pass`. SB03 and SB04 may enter independently; any provider need for unbounded work, leaked SDK/FileTools types, or false capabilities reopens SB02.
