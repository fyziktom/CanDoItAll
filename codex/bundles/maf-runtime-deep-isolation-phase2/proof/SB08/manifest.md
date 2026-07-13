# SB08 Proof Manifest

## Status

- Implemented with focused closure proof.

## Owned Requirements

- N001, N003, N005, N006, N007
- MAF2-R009, MAF2-R013, MAF2-R014

## Semantic Invariant Contract

- `bundle://proof/SB08/semantic-invariants.md`

## Changed-File Manifest

- `bundle://proof/SB08/changed-file-hashes.md`

## Command Transcripts

| Evidence | Transcript | Result |
| --- | --- | --- |
| Final boundary scan | `bundle://proof/SB08/transcripts/source-boundary-scans.txt` | Single `MafAgentRuntime.cs`; no runtime partial declaration, forbidden `MafAgentRuntime.Capabilities*`, nested private runtime types, owner-builder, or builder-this patterns. |
| MAF project build | `bundle://proof/SB08/transcripts/maf-project-build.txt` | ExitCode 0. |
| Focused runtime unit suite | `bundle://proof/SB08/transcripts/focused-unit-tests.txt` | ExitCode 0; 151 passed. |
| Handoff integration smoke | `bundle://proof/SB08/transcripts/handoff-integration-tests.txt` | ExitCode 0; 3 passed. |
| Performance/startup boundary | `bundle://proof/SB08/transcripts/performance-boundary-check.txt` | ExitCode 0; no sync-blocking matches in extracted runtime paths; command durations recorded. |

## Source Assertions

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs` owns capability-state composition and stage metrics.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs` owns Microsoft Agent Framework agent construction, handoff build composition, tool policy instrumentation, and finalizer-tool attachment.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/ContextCapabilityBuilder.cs`, `SkillCapabilityBuilder.cs`, `ToolCapabilityBuilder.cs`, and `McpCapabilityBuilder.cs` are top-level builders instead of nested runtime classes.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Input/InputAttachmentPreparer.cs` owns request-scoped input attachment preparation.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Input/InputAttachmentSupport.cs`, `RequestScopedSessionContentScrubber.cs`, `MafRuntimeExecutionOptionsResolver.cs`, and `MafRuntimeToolInvocationResultClassifier.cs` own helper policies that tests previously reached through `MafAgentRuntime`.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Execution/ProcessArtifactRecoveryService.cs` owns process-artifact finalizer recovery parsing.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/ProviderRuntimeDiagnostics.cs` owns provider health/test-message helpers.
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafRuntimeArchitectureServicesTests.cs` prevents reintroducing hidden runtime partials/nested capability builders and private reflection for composition methods.

## Anti-Stub Audit

- `bundle://proof/SB08/transcripts/source-boundary-scans.txt` proves no forbidden runtime-owned capability implementation remains under `MafAgentRuntime`.
- `bundle://proof/SB08/transcripts/performance-boundary-check.txt` scans the extracted runtime paths for sync-blocking startup anti-patterns.
- No implementation path added `TODO`, `NotImplementedException`, or fixture-specific branching for the moved runtime collaborators.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Runtime capability composition | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs` delegates to the composer | `bundle://proof/SB08/transcripts/focused-unit-tests.txt` | Architecture guard fails on hidden composition methods and forbidden partial files. |
| Hosted-agent construction | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` delegates public hosted-agent creation and runtime builds to the factory | `bundle://proof/SB08/transcripts/handoff-integration-tests.txt` | `MafAgentRuntime_is_not_a_split_partial_namespace` fails if the factory is hidden back inside a runtime partial. |
| Input/session helper policy | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Input/InputAttachmentSupport.cs` and `RequestScopedSessionContentScrubber.cs` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` calls the helpers during run preparation and session persistence | `bundle://proof/SB08/transcripts/focused-unit-tests.txt` | Attachment tests target extracted helpers directly without `MafAgentRuntime` reflection/static helper coupling. |
| Extracted process-artifact recovery | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Execution/ProcessArtifactRecoveryService.cs` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` calls the service for finalizer recovery | `bundle://proof/SB08/transcripts/focused-unit-tests.txt` | `AgentFinalizerPolicyTests` reject invalid/conflicting branch outcome artifacts. |
| Composition metrics | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeServiceCollectionExtensions.cs` metrics sink | `bundle://proof/SB08/transcripts/performance-boundary-check.txt` | Source assertion verifies stage-level measurement calls remain. |

## Residuals

- Full repository test suites were not run. The bundle's original scope exception already called out unrelated full-suite baseline failures; final proof uses focused runtime unit coverage and handoff integration smoke.
- `MafAgentRuntime.cs` remains the public execution adapter and still contains the live provider run loop/session persistence orchestration. The refactor removed runtime partials, hosted-agent construction, hidden builders/config/plugins/input/provider/recovery helpers, and direct test coupling to runtime helper statics; a future phase can still extract a dedicated execution coordinator if desired.
