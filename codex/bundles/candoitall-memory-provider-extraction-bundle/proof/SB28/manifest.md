# SB28 Proof Manifest

## Status

- Subbundle: `SB28`
- Status: `Completed`
- Owned requirements: `R15`, `R16`
- Owned raw notes: native curator/professor/self-regulation integration, memory-initiated verification policy, native MAF abstraction boundary, provider event emission, loop guard.

## Native Repository Context

- Native repo alias: `native-repo://`
- Native repo local root for this execution: `C:\repositories\CanDoItAll.CognitiveMemory`
- Scope note: SB28 implements native-owned curator, professor, self-regulation, and context contribution flows in `CanDoItAll.CognitiveMemory.Maf`. It deliberately does not port host-coupled `AppDbContext`/workspace-service implementations from the main module and does not register native memory through the main Agent module.

## Semantic Invariant Contract

- Contract: `bundle://proof/SB28/semantic-invariants.md`

## Changed File Hashes

The changed file hash inventory is captured in `bundle://proof/SB28/transcripts/native-file-hashes.txt`.

| File | After SHA-256 |
| --- | --- |
| `native-repo://src/CanDoItAll.CognitiveMemory.Maf/CanDoItAll.CognitiveMemory.Maf.csproj` | `bb818840f43d28bce90f7e0d32bcdd5414c52797748f9f95ace33fb83e41ab25` |
| `native-repo://src/CanDoItAll.CognitiveMemory.Maf/README.md` | `1d99d5bd2783e92c7a6ff785dac3727239c9ba63a23ddb75251db18040029ec7` |
| `native-repo://src/CanDoItAll.CognitiveMemory.Maf/CognitiveMemoryNativeMafContracts.cs` | `5043ece32676e43fe30326b50b2951330ed081ef187d8483fbf4c2dd17554419` |
| `native-repo://src/CanDoItAll.CognitiveMemory.Maf/CognitiveMemoryNativeMafServices.cs` | `5b1bfdbac27a3382573d073efdfe55d2013152b8d8eaf62ed6a6435d1e9edb3b` |
| `native-repo://src/CanDoItAll.CognitiveMemory.Maf/CognitiveMemoryNativeContextContributor.cs` | `d74085896735c94b47d2885b2ab36b44cf804283ee7cb22dce290ae8178f9b9b` |
| `native-repo://tests/CanDoItAll.CognitiveMemory.Tests/CanDoItAll.CognitiveMemory.Tests.csproj` | `825be0ff94ff2307357b3be6f04141d45479fd33c20d5d64c8a8c6f86db09599` |
| `native-repo://tests/CanDoItAll.CognitiveMemory.Tests/NativeMafIntegrationTests.cs` | `41e2fcc3c933785664886eb8de3b72238819a895a8e9457a3d111bdf386c15dc` |

## Command Transcripts

| Purpose | Transcript |
| --- | --- |
| Failing-first native MAF audit | `bundle://proof/SB28/transcripts/failing-first-native-maf-integration-audit.txt` |
| Focused native MAF integration tests | `bundle://proof/SB28/transcripts/passing-native-maf-integration-tests.txt` |
| Full native tests | `bundle://proof/SB28/transcripts/passing-native-tests.txt` |
| Native solution build | `bundle://proof/SB28/transcripts/passing-native-solution-build.txt` |
| Main CanDoItAll solution build | `bundle://proof/SB28/transcripts/passing-main-solution-build.txt` |
| Native MAF source boundary audit | `bundle://proof/SB28/transcripts/source-boundary-audit.txt` |
| Native MAF dependency boundary audit | `bundle://proof/SB28/transcripts/dependency-audit-native-maf-boundary.txt` |
| Positive MAF abstraction audit | `bundle://proof/SB28/transcripts/positive-maf-abstraction-audit.txt` |
| Anti-stub audit | `bundle://proof/SB28/transcripts/anti-stub-audit.txt` |
| Semantic invariant assertions | `bundle://proof/SB28/transcripts/semantic-invariant-assertions.txt` |
| Changed file hash inventory | `bundle://proof/SB28/transcripts/native-file-hashes.txt` |
| Bundle prepared-stage validation after SB28 | `bundle://evidence/33-prepared-stage-validation-after-sb28.txt` |
| Closure artifact path audit | `bundle://proof/SB28/transcripts/closure-artifact-path-audit.txt` |

## Proof Artifact Hashes

The closure path audit records portable `bundle://` paths and SHA-256 hashes for all required SB28 proof artifacts.

## Passing Proof

- Failing-first transcript: the SB27 state failed SB28 because the native MAF project had no curator, professor, self-regulation flow, policy gate, context contributor, verification event emission, or native MAF integration tests.
- Focused test transcript: exit code `0`; 6 native MAF integration tests pass for curator ingestion/event emission, professor verification policy denial and approval, self-regulation maintenance signal emission, provider re-entry loop denial, and context contribution through native recall.
- Full native tests transcript: exit code `0`; 25 tests pass across native persistence, engine, protocol API, and SB28 MAF integration.
- Native solution build transcript: exit code `0`; the native solution compiles with only existing NU1900 vulnerability-index fetch warnings.
- Main solution build transcript: exit code `0`; the main solution compiles with existing NU1900 vulnerability-index fetch warnings and NU1903 `Microsoft.OpenApi` advisory warnings only.
- Bundle prepared-stage validator transcript: exit code `0`; the edited bundle remains valid for stage `prepared`.
- Closure path audit transcript: exit code `0`; all SB28 manifest, semantic invariant, transcript, evidence, report, and status files exist.
- Boundary audits: native MAF production source/project files contain no host Web, composition, `AppDbContext`, or main module implementation references while positively referencing current MAF abstraction types.
- Anti-stub audit: SB28 implementation/test files contain no TODO, NotImplemented, stub, placeholder, or fake-only markers.

## Source Assertions

- `native-repo://src/CanDoItAll.CognitiveMemory.Maf/CognitiveMemoryNativeMafContracts.cs` defines native MAF request, decision, outcome, flow, and policy contracts.
- `native-repo://src/CanDoItAll.CognitiveMemory.Maf/CognitiveMemoryNativeMafServices.cs` implements policy-gated curator, professor, and self-regulation flows over native application services and generic provider events.
- `native-repo://src/CanDoItAll.CognitiveMemory.Maf/CognitiveMemoryNativeContextContributor.cs` implements `IAgentContextContributor` over native recall and exposes `AddCognitiveMemoryNativeMaf`.
- `native-repo://tests/CanDoItAll.CognitiveMemory.Tests/NativeMafIntegrationTests.cs` exercises production DI with native in-memory persistence rather than hand-built output DTOs.
- `native-repo://src/CanDoItAll.CognitiveMemory.Maf/CanDoItAll.CognitiveMemory.Maf.csproj` references the current MAF abstraction project and does not reference `CanDoItAll.Modules.AgentFramework`.

## Browser Validation

- N/A. SB28 adds native service/MAF integration code and tests, not a browser-visible host or UI surface.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Native MAF policy gate | `CognitiveMemoryNativeMafPolicyGate` | Curator/professor/self-regulation tests | Allows explicit native flows and blocks unsafe re-entry or disabled verification/launch requests | `PolicyGate_RejectsProviderReentryLoop` and professor denial test |
| Curator flow | `CognitiveMemoryNativeCuratorFlow` | Curator integration test | Ingests trusted curator memory through native ingestion and emits a generic verification request event | Failing-first audit rejects missing flow/event kind |
| Professor flow | `CognitiveMemoryNativeProfessorFlow` | Professor approval and denial tests | Reads native diagnostics and emits verification requests only when policy permits | Verification-disabled denial test |
| Self-regulation flow | `CognitiveMemoryNativeSelfRegulationFlow` | Self-regulation integration test | Converts high-risk native pending-review state into a generic maintenance signal | Failing-first audit and semantic assertions reject missing maintenance event |
| Native MAF context contributor | `CognitiveMemoryNativeContextContributor` | Context contributor integration test | Uses native recall behind `IAgentContextContributor` without main Agent module storage | Boundary audit fails on main module or host composition coupling |
| Native MAF DI registration | `AddCognitiveMemoryNativeMaf` | Focused native MAF tests through production DI | Registers policy/options as singleton and native flows/context contributor as scoped services | Initial failing test attempt exposed scoped/native lifetime mismatch and was fixed |
| Boundary artifacts | Source/dependency audits | Native/main builds and tests | Native MAF package depends on native services, generic memory contracts, and current MAF abstractions only | Audits fail on host persistence, Web, composition, or main module dependencies |

## Closure Decision

- SB28 closure gate: `Pass` after prepared validator and closure path audit hashes are recorded.
- Reopened subbundles: `None`.
- Downstream permission: SB29 may start because native advanced MAF behavior is now owned by the native service boundary and policy-gated through generic provider events.
