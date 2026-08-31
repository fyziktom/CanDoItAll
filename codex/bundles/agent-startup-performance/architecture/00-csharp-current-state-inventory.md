# C# Current-State Inventory

Read-only source inspection plus CodeAnalytics `snap-20260831122755-8dc56aa3`; see `analysis/codeanalytics-summary.json`. Snapshot has5projects/583documents, only informational DI/EF diagnostics, no project cycle in selected graph. Existing2module/2type cycles and large-file findings are baseline; no broad cleanup planned.

| Owner / exact repo-relative file | Responsibility and construction |
|---|---|
| `src/Foundation/CanDoItAll.Infrastructure/FileSystem/PhysicalFileSystemPathPolicy.cs` | Factory Create; concrete policy ctor/root containment/reparse checks/DetectCaseSensitivity. Factory has no injected dependencies; concrete ctor has root plus optional internal case fact. |
| `src/Foundation/CanDoItAll.Infrastructure/FileSystem/DurableFileWriter.cs` | Atomic durable replacement/directory safety. One public factory dependency; internal observer is a second constructor parameter for test seams. |
| `src/Foundation/CanDoItAll.Infrastructure.Abstractions/FileSystem/PhysicalFileSystemPathContracts.cs` | Existing public policy/factory contracts; inspect consumers if any signature change is proposed. No change planned. |
| `src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceStore.cs` | Existing partial owner, 2271lines in main file. Public root/scope2arguments; richest internal ctor8arguments (root/scope plus diagnostic/boundary hooks). Directly constructs layout, policy factory, writer, JSON store, slice/projection store, cross-process lock and history journal. |
| `src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceExecutionSliceStore.cs` | Internal sealed, 3372lines; layout+JSON2dependencies; run slices/usage projections/prepared commit plans/recovery checks. |
| `src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceChatProjectionStore.cs` | Internal sealed, 1542lines; layout+JSON2dependencies; session/run read-model projection and validation. |
| `src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceJsonStore.cs` | Internal sealed, 436lines; full ctor5parameters (diagnostic, factory, writer, root, history journal); actual atomic JSON compare/write boundary. |
| `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedAwareProviderRuntimeProfileSnapshotLoader.cs` | DatabaseProviderRuntimeProfileSnapshotLoader has5dependencies: DbContext factory, personal/shared mappers, shared materializer, initialization options. Reads revision and full profiles. |
| `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Helpers.cs` | AppendExecutionLogAsync awaits persisted update; unchanged caller/ordering oracle. |

Provider materializer/cache exact sources and tests are linked in SB02. Existing runtime composition remains. Existing partials are not made into new boundaries; no nested behavior container or new partial is allowed.

Current/missing tests: [test selection](../plan/test-selection.md) inventories existing classes. New characterization is needed for operation-local freshness, unchanged-token corruption parity, immediate-vs-recovery read counts and projection equivalence. Counts are static inventory only. Direct helper tests must not construct the whole application. Existing real store/integration fixtures supply boundary/cancellation/fault seams; live proof supplements rather than replaces them.
