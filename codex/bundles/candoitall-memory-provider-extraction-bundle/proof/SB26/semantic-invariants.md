# SB26 Semantic Invariants

## Raw Note Closure

- Native Cognitive Memory now owns core application services for ingestion, recall, consolidation, quality review, probing, temporal replay, self-regulation, and professor diagnostics.
- Native engine services use native contracts and `CognitiveMemoryDbContext`; they do not receive host `AppDbContext` instances or main module services.
- The old source-reference services remain in the main repo until SB30/SB31 because they are host-coupled. SB26 establishes the native behavior required for SB27/SB28/SB29 to continue.
- SB26 does not expose remote APIs, register host composition, migrate existing host data, or wire MAF professor tools. Those remain owned by SB27, SB28, SB30, and SB31.

## Shipped Behavior

- Application services validate inputs explicitly and fail predictably on invalid requests.
- The EF-backed native engine store creates native source, memory, link, review, run, and recall trace records.
- Recall returns native memory records with source references and writes trace state.
- Consolidation converts unlinked native source items into native memory records and review items when risk requires it.
- Probe, temporal replay, self-regulation, and professor diagnostics read persisted native state rather than manually seeded DTOs.

## Invariants

### SB26-I01 Native Engine Service Surface

- Source raw note: native recall, ingestion, consolidation, and related application services must move into the native repo.
- Expected behavior: native application contracts expose ingestion, recall, consolidation, review, probe, temporal replay, self-regulation, and professor diagnostics.
- Disallowed shallow implementation: DTO-only contracts, renamed old module classes, or a single generic method that hides behavior behind strings.
- Failing-first test and transcript: `bundle://proof/SB26/transcripts/failing-first-native-engine-audit.txt`.
- Passing test and transcript: `bundle://proof/SB26/transcripts/passing-native-engine-tests.txt` and `bundle://proof/SB26/transcripts/semantic-invariant-assertions.txt`.
- Changed source files and hashes: `native-repo://src/CanDoItAll.CognitiveMemory.Application/CognitiveMemoryEngineContracts.cs` `09d0692f1994011e132a53d9c05f1e088b9cb0f1e4c0f6b4a3ae6ceb988ab932`.
- Production assertions: `SB26-I01` through `SB26-I05` appear in `bundle://proof/SB26/transcripts/semantic-invariant-assertions.txt`.
- Red-team negative case: deleting the native service interfaces fails semantic assertions and DI tests.
- Downstream dependency check: SB27 can expose the contracts through native protocol APIs.

### SB26-I02 Native EF Engine Store

- Source raw note: migrated services must not reuse host persistence.
- Expected behavior: `EfCognitiveMemoryEngineStore` uses `IDbContextFactory<CognitiveMemoryDbContext>` and native records only.
- Disallowed shallow implementation: host `AppDbContext`, in-memory dictionaries, or repository stubs that do not persist source links/review/run/trace state.
- Failing-first test and transcript: `bundle://proof/SB26/transcripts/failing-first-native-engine-audit.txt`.
- Passing test and transcript: `bundle://proof/SB26/transcripts/passing-native-engine-tests.txt`, `bundle://proof/SB26/transcripts/source-boundary-audit.txt`, and `bundle://proof/SB26/transcripts/semantic-invariant-assertions.txt`.
- Changed source files and hashes: `native-repo://src/CanDoItAll.CognitiveMemory.Persistence/EfCognitiveMemoryEngineStore.cs` `4fecaae5f2e89bbb4ae12c49a0311b33bc03675a9ec9f7ddba02683d63dfcbfb`.
- Production assertions: `SB26-I06` and `SB26-I09` appear in `bundle://proof/SB26/transcripts/semantic-invariant-assertions.txt`.
- Red-team negative case: adding `AppDbContext` or removing persisted source links/review/trace writes fails audits or tests.
- Downstream dependency check: SB29 can harden native service startup around a real store.

### SB26-I03 Ingestion And Recall Lifecycle

- Source raw note: recall must be source-grounded and observable, not a synthetic string response.
- Expected behavior: ingestion writes manifest/item/memory/link/run state; recall reads native records, returns source refs, and writes recall traces.
- Disallowed shallow implementation: manually constructing recall DTOs in tests or searching a local list without persisted source evidence.
- Failing-first test and transcript: `bundle://proof/SB26/transcripts/failing-first-native-engine-audit.txt`.
- Passing test and transcript: `bundle://proof/SB26/transcripts/passing-native-engine-tests.txt`.
- Changed source files and hashes: `native-repo://tests/CanDoItAll.CognitiveMemory.Tests/NativeEngineServiceTests.cs` `7accfb469b1e3dad97cdb19a4843e94b18392329ca24404cbd9c734da388d0de`.
- Production assertions: the test `IngestionAndRecall_UseNativePersistenceWithoutHostDbContext` appears in `bundle://proof/SB26/transcripts/passing-native-engine-tests.txt`.
- Red-team negative case: removing source link creation or recall trace persistence fails tests/assertions.
- Downstream dependency check: SB27 remote provider behavior can depend on native recall results with evidence.

### SB26-I04 Consolidation And Review Lifecycle

- Source raw note: consolidation and quality review must become native service behavior.
- Expected behavior: consolidation creates native memory records from unlinked source items; high-risk items create review work; review decisions update review and memory validation state.
- Disallowed shallow implementation: consolidation that returns counts only, review decisions that update DTOs only, or high-risk content accepted without review state.
- Failing-first test and transcript: `bundle://proof/SB26/transcripts/failing-first-native-engine-audit.txt`.
- Passing test and transcript: `bundle://proof/SB26/transcripts/passing-native-engine-tests.txt`.
- Changed source files and hashes: `native-repo://src/CanDoItAll.CognitiveMemory.Persistence/EfCognitiveMemoryEngineStore.cs` `4fecaae5f2e89bbb4ae12c49a0311b33bc03675a9ec9f7ddba02683d63dfcbfb`.
- Production assertions: the tests `Consolidation_CreatesNativeMemoryRecordsForUnlinkedSourceItems` and `QualityReviewAndProbe_UpdateNativeState` appear in the passing transcript.
- Red-team negative case: removing review item creation or memory validation updates fails tests.
- Downstream dependency check: SB31 can migrate/retire host data into a native review-compatible schema.

### SB26-I05 Professor, Temporal Replay, And Self-Regulation

- Source raw note: probing, professor, temporal replay, and self-regulation behavior must have native service targets.
- Expected behavior: temporal replay reads native run/trace state, self-regulation blocks high-risk recall while review is pending, and professor diagnostics surfaces native pending review work.
- Disallowed shallow implementation: returning fixed recommendations, relying on test-only seeded signals, or deferring every professor-related contract to SB28.
- Failing-first test and transcript: `bundle://proof/SB26/transcripts/failing-first-native-engine-audit.txt`.
- Passing test and transcript: `bundle://proof/SB26/transcripts/passing-native-engine-tests.txt`.
- Changed source files and hashes: `native-repo://src/CanDoItAll.CognitiveMemory.Application/CognitiveMemoryEngineContracts.cs` `09d0692f1994011e132a53d9c05f1e088b9cb0f1e4c0f6b4a3ae6ceb988ab932`; `native-repo://tests/CanDoItAll.CognitiveMemory.Tests/NativeEngineServiceTests.cs` `7accfb469b1e3dad97cdb19a4843e94b18392329ca24404cbd9c734da388d0de`.
- Production assertions: the test `ProfessorTemporalReplayAndSelfRegulation_ReadNativeOperationalState` appears in the passing transcript.
- Red-team negative case: removing persisted run replay, high-risk recall blocking, or pending-review diagnostics fails the test.
- Downstream dependency check: SB28 can wire MAF professor/curator integration to native diagnostics instead of host services.

### SB26-I06 Boundary And Optional Projection Isolation

- Source raw note: native engine migration must not introduce host dependencies or base Qdrant runtime requirements.
- Expected behavior: native production source has no host persistence/main module references; existing projection tests keep Qdrant optional.
- Disallowed shallow implementation: adding main module project references, host EF references, or Qdrant as a base runtime dependency.
- Failing-first test and transcript: `bundle://proof/SB26/transcripts/failing-first-native-engine-audit.txt`.
- Passing test and transcript: `bundle://proof/SB26/transcripts/source-boundary-audit.txt` and `bundle://proof/SB26/transcripts/passing-native-engine-tests.txt`.
- Changed source files and hashes: `native-repo://src/CanDoItAll.CognitiveMemory.Persistence/CanDoItAll.CognitiveMemory.Persistence.csproj` `e63abe7a48080a34cfe448f086db284fedcd542ae4c12fccc285e5b6ed2eb92f`.
- Production assertions: native architecture tests and the source-boundary audit pass.
- Red-team negative case: adding `CanDoItAll.Infrastructure`, `AppDbContext`, or main module references fails tests/audits.
- Downstream dependency check: SB30 can remove host composition dependencies after native API/provider wiring lands.

## Shallow-Pass Trap Rejections

- A copied host service fails source-boundary audits because it imports host persistence and main module services.
- A DTO-only implementation fails because tests resolve production DI and assert persisted records.
- An in-memory-only shortcut fails because recall, review, replay, and diagnostics are read back through native EF state.
- A fixed professor/probe recommendation fails because tests require pending native review state to drive decisions.
- A Qdrant-required base runtime path fails the existing native architecture guard.

## Adversarial Negative Proof

- Failing-first audit rejects missing native engine interfaces and store.
- Semantic assertions reject missing contracts, DI registration, persisted source links, review writes, recall traces, or host-free store boundaries.
- Architecture tests reject host persistence/main module references in native production source.
- Anti-stub audit rejects TODO, NotImplemented, stub, placeholder, fake-only, test-only, and XML-inheritdoc markers.

## Semantic Positive Proof

- Native tests run through production DI registration with `AddCognitiveMemoryApplication()` and `AddCognitiveMemoryPersistence()`.
- Ingestion persists source and memory state through the native store; recall consumes it and writes traces.
- Consolidation, review, probe, replay, self-regulation, and professor diagnostics are exercised against native EF records.
- The main solution still builds after native engine migration.

## Downstream Dependency Check

- SB27 can expose native engine services as protocol APIs and remote provider behavior.
- SB28 can wire MAF curator/professor integration to native diagnostics and self-regulation instead of host services.
- SB29 can harden native startup around real engine services.
- SB30 can remove host composition/Qdrant/native cognitive dependencies after native API/provider paths are available.
- SB31 can migrate/export legacy host data into native engine-owned records.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Native engine contracts | `native-repo://src/CanDoItAll.CognitiveMemory.Application/CognitiveMemoryEngineContracts.cs` | Native DI and tests | Contracts are resolved through production service collection | Semantic assertions fail if contracts disappear |
| Native EF engine store | `native-repo://src/CanDoItAll.CognitiveMemory.Persistence/EfCognitiveMemoryEngineStore.cs` | Native engine tests | Store persists and queries native records | Source audit fails on host references; tests fail on missing persisted state |
| Ingestion/recall lifecycle | `IngestAsync` and `RecallAsync` | `IngestionAndRecall_UseNativePersistenceWithoutHostDbContext` | Source, memory, link, run, and trace records are created/consumed | Test fails if source evidence or recall trace is removed |
| Consolidation/review lifecycle | `ConsolidateAsync` and `DecideReviewAsync` | Consolidation and review/probe tests | Source item becomes memory/link/review; review updates validation | Tests fail if only counts/DTOs are returned |
| Probe diagnostics | `ProbeAsync` | Review/probe test | Counts native memory, source, review, and projection state | Test fails if pending review is ignored |
| Temporal replay | `ReplayAsync` | Professor/temporal/self-regulation test | Replays persisted runs/traces by time window | Test fails if persisted run state is ignored |
| Self-regulation | `EvaluateSelfRegulationAsync` | Professor/temporal/self-regulation test | High-risk pending review blocks recall | Test fails if high-risk review does not affect decision |
| Professor diagnostics | `DiagnoseProfessorAsync` | Professor/temporal/self-regulation test | Pending review state drives focus/recommendations | Test fails if diagnostics are fixed text |
| Host boundary | `source-boundary-audit.txt` | Architecture tests and native build | Native repo builds independently | Audit/tests fail on host persistence/main module references |
