# SB24 Semantic Invariants

## Raw Note Closure

- The live `CanDoItAll.CognitiveMemory` repository is no longer an unscaffolded shell. It now contains a native solution with service, workers, contracts, domain, persistence, application, projection, MAF, UI, and tests projects.
- Native Cognitive Memory remains optional and service-owned. The new service health endpoint starts without Qdrant and without the main CanDoItAll host.
- The native repository references generic Memory Protocol abstractions only at the contract boundary and does not depend on the main app composition, main modules, or host `AppDbContext`.
- SB24 intentionally does not migrate DB/domain/engine implementation. Those changes remain owned by SB25-SB29.

## Shipped Behavior

- `CanDoItAll.CognitiveMemory.slnx` builds from the native repo root.
- `CognitiveMemoryProtocolMetadata` exposes `memory.cognitive-native` manifest metadata, generic protocol capabilities, and a provider-owned UI surface key.
- `Program.cs` exposes `/health` with `Healthy`, `memory-protocol.v1`, `memory.cognitive-native`, and `qdrantRequired: false`.
- `CognitiveMemoryDbContext` owns the native `cognitive_memory_records` EF model and can persist/read native records through focused tests.
- `NativeScaffoldArchitectureTests` guard project layout, forbidden main repo dependencies, and base Qdrant package absence.
- `CognitiveMemoryProviderPanel.razor` provides an RCL surface scaffold without wiring it into the main app browser route.

## Invariants

### SB24-I01 Native Solution Scaffold

- Source raw note: create the real native repository solution/project structure before SB25-SB29 migrate code.
- Expected behavior: the native solution includes Contracts, Domain, Persistence, Application, Projection.Rag, Maf, Service, Workers, UI, and Tests projects.
- Disallowed shallow implementation: a README-only repo, a ZIP placeholder, or projects created outside `C:\repositories\CanDoItAll.CognitiveMemory`.
- Failing-first test and transcript: `bundle://proof/SB24/transcripts/failing-first-native-scaffold-audit.txt`.
- Passing test and transcript: `bundle://proof/SB24/transcripts/passing-native-solution-build.txt`, `bundle://proof/SB24/transcripts/passing-native-tests.txt`, and `bundle://proof/SB24/transcripts/semantic-invariant-assertions.txt`.
- Changed source files and hashes: `native-repo://CanDoItAll.CognitiveMemory.slnx` `3f664f0630d8913436ebc0cb9d481c879ee80af0dd68e3f981a44cecac684e38`; complete inventory in `bundle://proof/SB24/transcripts/native-file-hashes.txt`.
- Production assertions: `SB24-I01` appears in `bundle://proof/SB24/transcripts/semantic-invariant-assertions.txt`.
- Red-team negative case: deleting Service, Workers, UI, or Tests from the solution fails the invariant assertion and architecture guard test.
- Downstream dependency check: SB25 can add native persistence migration only because project boundaries and test project are now present.

### SB24-I02 Independent Health Endpoint

- Source raw note: native service must start independently and must not require Qdrant by default.
- Expected behavior: `/health` returns status, service name, protocol version, provider kind, and `qdrantRequired: false`.
- Disallowed shallow implementation: a DTO-only response test, a health endpoint hosted by the main CanDoItAll app, or startup that requires Qdrant.
- Failing-first test and transcript: `bundle://proof/SB24/transcripts/failing-first-native-scaffold-audit.txt`.
- Passing test and transcript: `bundle://proof/SB24/transcripts/passing-native-health-endpoint.txt` and `bundle://proof/SB24/transcripts/semantic-invariant-assertions.txt`.
- Changed source files and hashes: `native-repo://src/CanDoItAll.CognitiveMemory.Service/Program.cs` `5f67581def62d02917f77372c7e7098cb283f6430557daf2b02e67907808581c`; `native-repo://src/CanDoItAll.CognitiveMemory.Contracts/CognitiveMemoryHealthResponse.cs` `e74f05f03eec1f88e1c6abcf1455b7138351bfde524689245a735dc8cb585ae8`.
- Production assertions: `SB24-I02` appears in `bundle://proof/SB24/transcripts/semantic-invariant-assertions.txt`.
- Red-team negative case: changing `QdrantRequired` to true or requiring the main host fails the health proof/source audit.
- Downstream dependency check: SB27 can add remote provider APIs after this independent host skeleton is proven.

### SB24-I03 Generic Protocol Boundary

- Source raw note: native service should compile against generic Memory Protocol contracts instead of main app/native module implementations.
- Expected behavior: native Contracts references `CanDoItAll.Memory.Abstractions` and declares provider metadata through generic manifest types.
- Disallowed shallow implementation: copying stringly typed protocol JSON, referencing main app modules, or hardcoding generic UI/native tabs in the main Memory module.
- Failing-first test and transcript: `bundle://proof/SB24/transcripts/failing-first-native-scaffold-audit.txt`.
- Passing test and transcript: `bundle://proof/SB24/transcripts/passing-native-tests.txt` and `bundle://proof/SB24/transcripts/semantic-invariant-assertions.txt`.
- Changed source files and hashes: `native-repo://src/CanDoItAll.CognitiveMemory.Contracts/CanDoItAll.CognitiveMemory.Contracts.csproj` `7d310a8e3d72d9e7d70cef76b9ca43b64a0238bbe62e250478f88a5a85331912`; `native-repo://src/CanDoItAll.CognitiveMemory.Contracts/CognitiveMemoryProtocolMetadata.cs` `50718feab3e2c122cc5e5315d0110039c1efbaa8a0b7bc968ec57e3d26b65d7d`.
- Production assertions: `SB24-I03` appears in `bundle://proof/SB24/transcripts/semantic-invariant-assertions.txt`.
- Red-team negative case: removing generic memory abstraction reference or replacing typed metadata with ad hoc strings fails protocol mapping tests.
- Downstream dependency check: SB27 can implement the remote provider driver against the generic protocol contract.

### SB24-I04 Native Persistence Ownership

- Source raw note: native persistence must be service-owned and must not expose host EF entities or `AppDbContext`.
- Expected behavior: `CognitiveMemoryDbContext` owns a native `DbSet<CognitiveMemoryRecord>` and maps `cognitive_memory_records`; options choose InMemory/PostgreSQL explicitly.
- Disallowed shallow implementation: reusing host `AppDbContext`, in-memory-only shortcuts, or seeding test records without a native DbContext.
- Failing-first test and transcript: `bundle://proof/SB24/transcripts/failing-first-native-scaffold-audit.txt`.
- Passing test and transcript: `bundle://proof/SB24/transcripts/passing-native-tests.txt` and `bundle://proof/SB24/transcripts/semantic-invariant-assertions.txt`.
- Changed source files and hashes: `native-repo://src/CanDoItAll.CognitiveMemory.Persistence/CognitiveMemoryDbContext.cs` `6d70475dceabda9b778445867d182786edf4f1cc49d91aef3ed77db779562617`; `native-repo://src/CanDoItAll.CognitiveMemory.Persistence/CognitiveMemoryPersistenceServiceCollectionExtensions.cs` `ce30b1b8f3ec93859cdc959f6c860d99beb62ef2b08df269ce272a9ab035e028`.
- Production assertions: `SB24-I04` appears in `bundle://proof/SB24/transcripts/semantic-invariant-assertions.txt`.
- Red-team negative case: adding `AppDbContext` to native production source fails the invariant assertion, source audit, and architecture guard.
- Downstream dependency check: SB25 can migrate DB context/persistence into this owned boundary.

### SB24-I05 Main Host Dependency Boundary

- Source raw note: native repo must not depend on the main CanDoItAll app module, Agent module, or host composition.
- Expected behavior: production native source contains no `CanDoItAll.Composition`, `CanDoItAll.Modules.AgentFramework`, or `AppDbContext` references.
- Disallowed shallow implementation: compiling only because native projects reference main implementation modules or host EF types.
- Failing-first test and transcript: `bundle://proof/SB24/transcripts/failing-first-native-scaffold-audit.txt`.
- Passing test and transcript: `bundle://proof/SB24/transcripts/source-boundary-audit.txt`, `bundle://proof/SB24/transcripts/passing-native-tests.txt`, and `bundle://proof/SB24/transcripts/semantic-invariant-assertions.txt`.
- Changed source files and hashes: `native-repo://tests/CanDoItAll.CognitiveMemory.Tests/NativeScaffoldArchitectureTests.cs` `20a5e5034a4d4aeb68dc3896a49ff677d97108ca1be1a2676bc52bc99258cdfd`.
- Production assertions: `SB24-I05` appears in `bundle://proof/SB24/transcripts/semantic-invariant-assertions.txt`.
- Red-team negative case: adding a forbidden main-module/main-host reference fails source audit and architecture tests.
- Downstream dependency check: SB30 host decoupling can rely on native extraction not reintroducing main host coupling.

### SB24-I06 Optional RAG/Qdrant Boundary

- Source raw note: Qdrant must not become a base startup dependency.
- Expected behavior: Projection.Rag is present as an optional scaffold/config path; no native production project file references a Qdrant package.
- Disallowed shallow implementation: adding Qdrant client packages to the base service, requiring vector store config for `/health`, or hiding Qdrant as a fallback startup dependency.
- Failing-first test and transcript: `bundle://proof/SB24/transcripts/failing-first-native-scaffold-audit.txt`.
- Passing test and transcript: `bundle://proof/SB24/transcripts/source-boundary-audit.txt`, `bundle://proof/SB24/transcripts/passing-native-health-endpoint.txt`, and `bundle://proof/SB24/transcripts/semantic-invariant-assertions.txt`.
- Changed source files and hashes: `native-repo://src/CanDoItAll.CognitiveMemory.Projection.Rag/CognitiveMemoryRagProjectionOptions.cs` is included in `bundle://proof/SB24/transcripts/native-file-hashes.txt`.
- Production assertions: `SB24-I06` appears in `bundle://proof/SB24/transcripts/semantic-invariant-assertions.txt`.
- Red-team negative case: adding a Qdrant package reference fails the architecture guard and source audit.
- Downstream dependency check: SB29 can harden optional projection behavior without forcing Qdrant into base startup.

## Shallow-Pass Trap Rejections

- A scaffold that only adds files but does not build fails `bundle://proof/SB24/transcripts/passing-native-solution-build.txt`.
- A DTO-only health response fails the host-visible health endpoint proof because the transcript starts the real native service.
- A native repo that depends on main app modules fails source boundary audit and `NativeScaffoldArchitectureTests`.
- An in-memory-only persistence shortcut fails the native persistence test that uses the real `CognitiveMemoryDbContext` and production registration shape.
- A Qdrant-default service fails both `/health` proof and architecture guard tests.

## Adversarial Negative Proof

- The failing-first scaffold audit proves SB24 started from a README-only native repo and rejected the missing solution/projects.
- The source boundary audit fails on main app/module/AppDbContext references in native production source.
- The architecture tests fail if expected projects are removed from the native solution or if Qdrant appears as a base project package.
- The anti-stub audit fails on TODO, NotImplemented, stub, placeholder, fake-only, or test-only production markers.

## Semantic Positive Proof

- Native build/test commands run from `C:\repositories\CanDoItAll.CognitiveMemory`, not from a fixture-only folder.
- The native service is launched and queried over HTTP at `/health`.
- Protocol mapping tests exercise generic Memory Protocol contracts and provider manifest metadata.
- Persistence tests use the production native EF context and service registration rather than hand-built DTOs only.
- Main solution build still passes after adding the separate native repo scaffold.

## Downstream Dependency Check

- SB25 can start because native persistence has an owned project, DbContext, options, and tests.
- SB26 can migrate engine/domain code into a native Domain/Application boundary without depending on the main app host.
- SB27 can implement remote provider APIs against the generic manifest/protocol contract.
- SB28 can integrate native MAF packages after the native Maf project exists without re-coupling the main AgentFramework module.
- SB29 can harden optional projection/service behavior with Qdrant remaining optional.
- SB30 can remove host composition dependencies knowing the native repo has a service-owned startup path.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Native solution scaffold | `native-repo://CanDoItAll.CognitiveMemory.slnx` and `bundle://proof/SB24/transcripts/native-file-hashes.txt` | Native build/test transcripts | Root README dependency rules and commands | Failing-first scaffold audit |
| Native protocol manifest | `native-repo://src/CanDoItAll.CognitiveMemory.Contracts/CognitiveMemoryProtocolMetadata.cs` | Protocol mapping tests | Service and future provider driver use generic manifest shape | Protocol tests fail on missing provider kind/capabilities |
| Native service health endpoint | `native-repo://src/CanDoItAll.CognitiveMemory.Service/Program.cs` | Health endpoint transcript | Service startup registers application and persistence services | Health proof fails if Qdrant/main host is required |
| Native owned persistence skeleton | `native-repo://src/CanDoItAll.CognitiveMemory.Persistence/CognitiveMemoryDbContext.cs` | Native persistence tests | Explicit persistence options choose InMemory/PostgreSQL | Source audit/architecture tests fail on host `AppDbContext` |
| Optional RAG projection boundary | `native-repo://src/CanDoItAll.CognitiveMemory.Projection.Rag/CognitiveMemoryRagProjectionOptions.cs` | Source boundary audit | Projection remains optional and disabled by default | Architecture tests fail on base Qdrant package |
| Native provider UI package scaffold | `native-repo://src/CanDoItAll.CognitiveMemory.UI/CognitiveMemoryProviderPanel.razor` | Protocol metadata declares provider UI surface key | Future provider surface integration can use the generic UI host | Browser validation remains N/A until route integration |
| Dependency boundary guard tests | `native-repo://tests/CanDoItAll.CognitiveMemory.Tests/NativeScaffoldArchitectureTests.cs` | Passing native tests transcript | Guard stays in native test suite for later phases | Fails on main app/module/AppDbContext or base Qdrant references |
