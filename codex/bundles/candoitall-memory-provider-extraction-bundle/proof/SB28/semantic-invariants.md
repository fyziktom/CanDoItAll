# SB28 Semantic Invariants

## Raw Note Closure

- Native Cognitive Memory owns curator, professor, self-regulation, and context-contribution behavior inside the native repo.
- Native memory-initiated verification requests and maintenance signals route through generic provider events.
- Native MAF integration uses current MAF abstractions only and does not depend on the main Agent module implementation, host app composition, or host `AppDbContext`.
- SB28 does not migrate host data, change browser UI, or wire final host composition.

## Shipped Behavior

- `CognitiveMemoryNativeCuratorFlow` ingests trusted curator messages through native ingestion and emits a generic verification request event when policy allows it.
- `CognitiveMemoryNativeProfessorFlow` reads native professor diagnostics and emits verification requests only when pending review state exists and policy allows verification requests.
- `CognitiveMemoryNativeSelfRegulationFlow` invokes native self-regulation and emits a generic maintenance signal when native memory blocks recall for high-risk pending review state.
- `CognitiveMemoryNativeMafPolicyGate` blocks provider re-entry loops, loop hop overflow, disabled verification requests, and disabled agent/workflow launches.
- `CognitiveMemoryNativeContextContributor` implements `IAgentContextContributor` by querying native recall and formatting memory sections for MAF context.

## Invariants

### SB28-I01 Native MAF Boundary

- Source raw note: native curator/professor integrations must move to the native service MAF package using MAF abstractions only.
- Expected behavior: the native MAF project references current MAF abstraction/core contracts without referencing `CanDoItAll.Modules.AgentFramework`, app composition, host Web, or host `AppDbContext`.
- Disallowed shallow implementation: copied host module services, host persistence dependencies, or registering through the main Agent module implementation.
- Failing-first test and transcript: `bundle://proof/SB28/transcripts/failing-first-native-maf-integration-audit.txt`.
- Passing test and transcript: `bundle://proof/SB28/transcripts/source-boundary-audit.txt`, `bundle://proof/SB28/transcripts/dependency-audit-native-maf-boundary.txt`, and `bundle://proof/SB28/transcripts/positive-maf-abstraction-audit.txt`.
- Changed source files and hashes: `native-repo://src/CanDoItAll.CognitiveMemory.Maf/CanDoItAll.CognitiveMemory.Maf.csproj` `bb818840f43d28bce90f7e0d32bcdd5414c52797748f9f95ace33fb83e41ab25`.
- Production assertions: dependency audit confirms the `CanDoItAll.AgentFramework.Core` abstraction reference and rejects main Agent module, Web, composition, and host DbContext references.
- Red-team negative case: adding host or main module dependencies fails boundary audits before downstream subbundles can proceed.
- Downstream dependency check: SB30 can compose native memory remotely without reintroducing native advanced code into the host module.

### SB28-I02 Curator Flow And Verification Event

- Source raw note: curator behavior must be native-owned and exposed as provider events or protocol results according to policy.
- Expected behavior: trusted curator messages create native memory records through native ingestion and emit `MemoryProviderEventKind.VerificationRequest` when policy allows.
- Disallowed shallow implementation: emitting events without native ingestion, using hand-built DTO outputs, or bypassing policy.
- Failing-first test and transcript: `bundle://proof/SB28/transcripts/failing-first-native-maf-integration-audit.txt`.
- Passing test and transcript: `bundle://proof/SB28/transcripts/passing-native-maf-integration-tests.txt`.
- Changed source files and hashes: `native-repo://src/CanDoItAll.CognitiveMemory.Maf/CognitiveMemoryNativeMafServices.cs` `5b1bfdbac27a3382573d073efdfe55d2013152b8d8eaf62ed6a6435d1e9edb3b`.
- Production assertions: `CuratorFlow_CapturesTrustedMemoryAndEmitsVerificationEventWhenAllowed` resolves production DI, executes the native flow, queries emitted events, and verifies persisted recall text.
- Red-team negative case: removing native ingestion, verification event emission, or policy checks fails the focused integration test and semantic assertions.
- Downstream dependency check: SB33 can validate end-to-end native memory event behavior through generic memory/event surfaces.

### SB28-I03 Professor And Self-Regulation Events

- Source raw note: professor review and self-regulation must surface advanced native behavior without direct host module coupling.
- Expected behavior: professor diagnostics emit verification requests for pending review when policy allows, and self-regulation emits maintenance signals for high-risk blocked recall.
- Disallowed shallow implementation: fixed diagnostics, always-on verification requests, missing maintenance signal, or silent success when policy denies behavior.
- Failing-first test and transcript: `bundle://proof/SB28/transcripts/failing-first-native-maf-integration-audit.txt`.
- Passing test and transcript: `bundle://proof/SB28/transcripts/passing-native-maf-integration-tests.txt` and `bundle://proof/SB28/transcripts/semantic-invariant-assertions.txt`.
- Changed source files and hashes: `native-repo://tests/CanDoItAll.CognitiveMemory.Tests/NativeMafIntegrationTests.cs` `41e2fcc3c933785664886eb8de3b72238819a895a8e9457a3d111bdf386c15dc`.
- Production assertions: professor approval/denial tests and self-regulation maintenance tests exercise native services and generic provider event emission.
- Red-team negative case: disabling policy checks, removing pending-review diagnostics, or omitting maintenance events fails the focused tests.
- Downstream dependency check: SB29/SB33 can rely on typed native advanced event output instead of host-only side effects.

### SB28-I04 Loop And Capability Policy Guard

- Source raw note: memory-initiated verification and agent/workflow requests must be policy guarded and cannot create memory-agent-memory loops.
- Expected behavior: policy denies provider re-entry, loop hop overflow, disabled verification requests, and disabled launch requests with typed denied outcomes.
- Disallowed shallow implementation: hidden fallback, permissive default launch behavior, or string-only policy decisions.
- Failing-first test and transcript: `bundle://proof/SB28/transcripts/failing-first-native-maf-integration-audit.txt`.
- Passing test and transcript: `bundle://proof/SB28/transcripts/passing-native-maf-integration-tests.txt`.
- Changed source files and hashes: `native-repo://src/CanDoItAll.CognitiveMemory.Maf/CognitiveMemoryNativeMafContracts.cs` `5043ece32676e43fe30326b50b2951330ed081ef187d8483fbf4c2dd17554419`.
- Production assertions: `ProfessorFlow_DeniesVerificationEventWhenPolicyDisallowsRequests` and `PolicyGate_RejectsProviderReentryLoop` cover denied behavior.
- Red-team negative case: accepting provider-originated verification requests or silently allowing disabled verification fails focused tests.
- Downstream dependency check: generic memory event inbox/outbox can consume native events without unbounded recursive agent invocation.

### SB28-I05 Native Context Contribution

- Source raw note: native memory should be usable through MAF abstractions without main Agent module storage or direct native leakage into generic memory.
- Expected behavior: `CognitiveMemoryNativeContextContributor` implements `IAgentContextContributor`, uses native recall, and registers through `AddCognitiveMemoryNativeMaf`.
- Disallowed shallow implementation: static context text, direct main Agent module references, or singleton registration of scoped native persistence services.
- Failing-first test and transcript: `bundle://proof/SB28/transcripts/failing-first-native-maf-integration-audit.txt`.
- Passing test and transcript: `bundle://proof/SB28/transcripts/passing-native-maf-integration-tests.txt`.
- Changed source files and hashes: `native-repo://src/CanDoItAll.CognitiveMemory.Maf/CognitiveMemoryNativeContextContributor.cs` `d74085896735c94b47d2885b2ab36b44cf804283ee7cb22dce290ae8178f9b9b`.
- Production assertions: `ContextContributor_ProvidesNativeRecallThroughMafAbstraction` seeds through native ingestion, resolves `IAgentContextContributor`, and verifies recall-backed context.
- Red-team negative case: singleton lifetime over scoped native services failed the first focused test attempt and was fixed with scoped native flows/context contributor.
- Downstream dependency check: SB30 can keep host composition generic while native MAF behavior remains opt-in and native-owned.

## Shallow-Pass Trap Rejections

- A native MAF project with only empty contracts fails the failing-first audit and focused tests.
- Emitting provider events without native ingestion/diagnostics fails curator/professor integration tests.
- Policy that silently allows verification or provider re-entry fails negative tests.
- Main Agent module, host Web/composition, or host `AppDbContext` references fail source/dependency audits.
- Singleton registration over scoped native persistence services fails production-DI integration tests.

## Adversarial Negative Proof

- Failing-first audit rejects missing native curator/professor/self-regulation flows, missing policy gate, missing verification event kind usage, missing context contributor, missing DI registration, and missing tests.
- Professor policy denial test rejects verification event emission when verification requests are disabled.
- Loop guard test rejects provider-originated re-entry.
- Source and dependency audits reject host persistence/main module coupling.
- Anti-stub audit rejects TODO, NotImplemented, stub, placeholder, fake-only, and test-only markers.

## Semantic Positive Proof

- Focused tests execute native production DI over native in-memory persistence and use native ingestion, diagnostics, self-regulation, event polling, and recall paths.
- Native MAF project compiles as part of the native solution and the main solution still builds after referencing current MAF abstractions.
- Semantic assertion transcript confirms required flow classes, event kinds, context contributor, DI extension, and test coverage names.

## Downstream Dependency Check

- SB29 can harden native service deployment around native-owned MAF/protocol behavior.
- SB30 can remove host direct native module composition without losing advanced native MAF behavior.
- SB31 can migrate host data after native advanced behavior has a native-owned destination.
- SB33 can validate final end-to-end native remote memory behavior including event and context contribution paths.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Native MAF policy gate | `CognitiveMemoryNativeMafPolicyGate` | Focused native MAF tests | Gates verification, launch, hop, and provider-origin requests | Professor denial and provider re-entry tests |
| Curator verification event | `CognitiveMemoryNativeCuratorFlow` | Curator integration test | Native ingestion creates memory and emits verification request event | Failing-first audit rejects missing event emission |
| Professor verification event | `CognitiveMemoryNativeProfessorFlow` | Professor approval test | Pending-review diagnostics become policy-gated verification requests | Professor denial test rejects disabled verification |
| Self-regulation maintenance signal | `CognitiveMemoryNativeSelfRegulationFlow` | Self-regulation integration test | High-risk blocked recall becomes a generic maintenance event | Semantic assertions reject missing maintenance event kind |
| Native context contribution | `CognitiveMemoryNativeContextContributor` | Context contributor integration test | Native recall becomes MAF context through `IAgentContextContributor` | Boundary audits reject main Agent module coupling |
| Native MAF DI extension | `AddCognitiveMemoryNativeMaf` | Production DI tests | Scoped native services resolve with native persistence scopes | First focused attempt exposed and fixed invalid singleton/scoped lifetime |
| Boundary guard | Source/dependency audits | Native/main builds and tests | Native MAF package remains native-owned plus MAF abstraction-only | Audits fail on host/native boundary regressions |
