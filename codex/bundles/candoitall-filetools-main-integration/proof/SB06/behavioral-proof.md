# SB06 Behavioral Proof

## Decision

- Closure gate: `Pass`.
- Package provenance, typed boundaries, native Storage mapping, declarative composition, and dependency direction are sufficient for SB07 to rely on.
- No authorization, content endpoint, cache, module implementation, or UI authority was introduced.

## Package And Asset Intake

- All seven `ExternalPackages/CanDoItAll.FileTools.*.0.1.0.nupkg` SHA-256 values match `proof/SB01/package-hashes.sha256`.
- At SB06 closure the two projects resolved only `CanDoItAll.FileTools.Abstractions/0.1.0`. SB07 then added the exact `CanDoItAll.FileTools.FileInteraction.Core/0.1.0` package required by the governed content/save adapters; no provider, renderer, or component package was imported speculatively.
- The Abstractions package contains its assembly, XML metadata, README, and NuGet metadata and declares zero `wwwroot` or `staticwebassets` entries. Consequently there is no FileTools static web asset to publish in this non-UI phase. Component-package asset validation remains assigned to the first UI adoption phase.
- The stale same-version global package cache was removed only for the resolved FileTools `0.1.0` path before forced restore, ensuring the build consumed the SB01-repacked ProviderNative contract.

## Boundary And Mapping Behavior

- `CanDoItAll.FileTools.Integration.Abstractions` owns validated semantic scope/root, browse session, native work-limit, binding, access, and known-file interaction contracts. It has no project reference.
- `FileToolsKnownFileContracts.cs` uses only FileInteraction contracts and contains no FileBrowser reference, preserving zero-browser known-file construction.
- `CanDoItAll.FileTools.Integration` owns opaque key mapping, Storage adapter/mapping, session construction, and one DI extension. It references only Integration.Abstractions and Infrastructure.
- The adapter forwards provider-native ordering, continuation, consistency, completeness, metadata requests, and all five native work-budget dimensions. Unsupported global sort/filter/recursive requests fail before native I/O.
- Descriptive browse keys and native read facts do not grant `Open`; content authority is intentionally deferred to SB07 authorization handles.
- The session declares ProviderNative ascending order with no folders-first reordering, so the FileTools runtime cannot accidentally replace bounded provider order with global client sorting.

## Behavioral Tests

Command:

```text
dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --filter FullyQualifiedName~FileToolsIntegrationBoundaryTests --no-restore
```

Result: `8 passed, 0 failed`.

Covered behavior:

- budget/order/completeness/cursor/consistency positive mapping;
- unsupported global ordering fails before native I/O;
- stale native cursor maps to a safe typed error without provider details;
- duplicate semantic binding is rejected;
- unknown storage binding is rejected;
- provider/storage mismatch is rejected;
- browse facts do not grant content authority;
- declarative registration resolves and creates a provider-native session.

## Build And Formatting

- Forced package restore passed after cache provenance reset.
- Integration implementation Release build with warnings as errors passed with zero warnings.
- Composition Release build with warnings as errors passed with zero warnings.
- Focused `dotnet format` completed for both new projects.

## Dependency And Architecture Proof

- Original SB06 scoped CodeAnalytics snapshot: `snap-20260713033459-65a8abd8`. Current SB07 re-entry snapshot: `snap-20260713042852-baab347b`.
- Scope: Infrastructure, Integration.Abstractions, Integration, Composition; 4 projects, 90 documents, 285 types, 1,667 members.
- Project graph is exactly Composition -> Integration; Composition -> Infrastructure; Integration -> Integration.Abstractions; Integration -> Infrastructure.
- No project cycle or forbidden reverse edge exists. The one reported module cycle is the pre-existing Infrastructure Persistence/ControlPlane cycle recorded at baseline.
- Five diagnostics are informational only: three pre-existing partially interpreted Infrastructure factory registrations and two Mermaid truncation notices.
- The initial snapshot flagged the 364-line adapter. Mapping was extracted as a cohesive internal collaborator without an artificial interface; the adapter is now 221 lines and the mapping collaborator 157 lines. The refreshed snapshot has no large-file finding in Integration.

## Source Hashes

```text
08ac416066ed993eebe44e5fbcc76ab008bbcfd3684c717624ab060071dd3760 *src/Integration/CanDoItAll.FileTools.Integration.Abstractions/FileToolsBrowseContracts.cs
56ea358607a78a42cb08d6b1abc32462ae3b655f9f2a0aa502f127520ea75d97 *src/Integration/CanDoItAll.FileTools.Integration.Abstractions/FileToolsKnownFileContracts.cs
8074f733f23a1e6f6337476196957b67c0cb2c48e970af334cda291fb19ccefb *src/Integration/CanDoItAll.FileTools.Integration.Abstractions/FileToolsSemanticScope.cs
3c65c508f86d6e56c80bdbb5f4b2345e3ed96b5eef34914168526c96ceee350e *src/Integration/CanDoItAll.FileTools.Integration/FileToolsIntegrationServiceCollectionExtensions.cs
f4c10f7c93fca1f559323d0acadf0ef770b256fda6bce8dc2856c942e5149325 *src/Integration/CanDoItAll.FileTools.Integration/StorageFileBrowserKeyCodec.cs
04953e5c961af49d464afe89e1d103daf4c9a1f24de40462158001d914280e84 *src/Integration/CanDoItAll.FileTools.Integration/StorageFileBrowserMapping.cs
9622683c8d1a44a9daf49b63665aaa1fb191279209834aa9b85aa75041346b8a *src/Integration/CanDoItAll.FileTools.Integration/StorageFileBrowserProvider.cs
110d768a7052294e34cdab151956cc5c7dcc578fd85729f26c8659f2e7017dd0 *src/Integration/CanDoItAll.FileTools.Integration/StorageFileToolsBrowseSessionFactory.cs
2bd70ed5c2151965fb3450bdf97253ff7e258c53fc04633db70b13eb9c5bee52 *tests/Unit/CanDoItAll.Tests.Unit/FileToolsIntegrationBoundaryTests.cs
```

## Risks And Progression

- Risk deferred by design: content/open/save capability remains absent until SB07 proves server-side authorization handles and endpoint enforcement.
- Risk deferred by design: FileTools component static assets are not referenced until UI work; their host asset smoke remains mandatory at that intake point.
- Progression: SB07 is unlocked.
