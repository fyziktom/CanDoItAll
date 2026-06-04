# SB03 Proof Manifest

## Subbundle

- ID: SB03
- Title: MAF registered tool-provider composition
- Status: Completed
- Critical foundation: Yes
- Owned requirements: RQ-003, RQ-007, RQ-008, RQ-014
- Raw notes: "rozplest ty zavislosti"; "po mensich krocich"; "nesmi veci zjednodusit nebo neco vynechat"
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`

## Changed Files With Hashes

| File | SHA-256 | Reason |
| --- | --- | --- |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs` | `FC5554CFE1FF951D7BBAF5997550225335DF2FEA617E27AF9D36A88A692790F3` | Adds registered runtime tool provider composition. |
| `repo://tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj` | `D508D52B6CB8933A9F710CA50EEF8A12BDF8056EC5E2DF517147C90946CD3E37` | Adds explicit Tooling test reference. |
| `repo://tests/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs` | `A306B3F3835736DF1E2C88136C7082F81156BB730B9FAE207E11985968A91292` | Adds zero-provider, fake provider, duplicate, approval-wrapper, and provider-failure tests. |
| Before/after hash transcript | `bundle://proof/SB03/source-assertions/changed-file-hashes.txt` | Full before/after hash evidence for touched files. |

## Commands

| Command | Transcript path | Exit code | Purpose |
| --- | --- | ---: | --- |
| `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~MafAgentRuntimeToolProviderComposition"` | `bundle://proof/SB03/transcripts/maf-tool-provider-composition-tests.txt` | 0 | Proves provider composition behavior. |
| `dotnet build CanDoItAll.slnx` | `bundle://proof/SB03/transcripts/solution-build.txt` | 0 | Proves solution builds after composition changes. |

## Validator Proof Citations

- Adversarial negative proof: N/A process/non-production preserved failing-first transcript; duplicate-provider and provider-failure tests are the maintained regression proof.
- Passing transcript: `bundle://proof/SB03/transcripts/maf-tool-provider-composition-tests.txt`.
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`.

## Source Assertions

| Assertion | Source path | Result |
| --- | --- | --- |
| MAF resolves and invokes registered runtime tool providers. | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`; `bundle://proof/SB03/source-assertions/provider-composition-source-audit.txt` | `AttachRegisteredRuntimeToolProvidersAsync` and `IAgentRuntimeToolProvider` usage exist. |
| Provider tool ordering is deterministic. | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`; `bundle://proof/SB03/transcripts/maf-tool-provider-composition-tests.txt` | Providers are ordered by `Order` then type name; test proves early/late ordering. |
| Provider duplicate names fail explicitly. | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`; `bundle://proof/SB03/transcripts/maf-tool-provider-composition-tests.txt` | Duplicate provider tool names throw with provider diagnostics instead of silent shadowing. |
| Provider mutation tools use existing approval wrapping policy. | `bundle://proof/SB03/transcripts/maf-tool-provider-composition-tests.txt` | `processes_run_start` is wrapped; `processes_runs_list` is not. |
| Old process tool path remains for compatibility. | `bundle://proof/SB03/source-assertions/old-process-path-still-present.txt` | MAF still contains `AttachInternalProcessToolsAsync`, `ProcessToolBuilder`, and the temporary Processes project reference. |
| Dispatcher unchanged. | `bundle://proof/SB03/source-assertions/dispatcher-unchanged.txt` | No dispatcher files in diff. |
| Anti-stub audit passed. | `bundle://proof/SB03/source-assertions/anti-stub-audit.txt` | No `TODO`, `NotImplemented`, or `NotImplementedException` stubs in SB03 source/test changes. |

## Semantic Adequacy Gate

| Label | Evidence |
| --- | --- |
| Raw note owned | Decouple through small steps without simplifying, omitting, or silently changing process approval behavior. |
| Shipped behavior | MAF now composes registered provider-neutral runtime tools while the old internal process tool builder remains available for compatibility. |
| Source proof | `bundle://proof/SB03/source-assertions/provider-composition-source-audit.txt` and `bundle://proof/SB03/source-assertions/old-process-path-still-present.txt`. |
| Test proof | `bundle://proof/SB03/transcripts/maf-tool-provider-composition-tests.txt` and `bundle://proof/SB03/transcripts/solution-build.txt`. |
| Shallow-pass trap | Adding the interface but never invoking DI providers would compile and leave SB04 unable to migrate process tools. |
| Adversarial negative proof | Duplicate tool names are rejected with provider diagnostics; a silent dedupe/shadow implementation fails the duplicate test. |
| Semantic positive proof | Fake providers contribute tools in deterministic order, zero providers do not fail, provider failures include provider type, and mutation tools are approval-wrapped. |
| Anti-stub audit | `bundle://proof/SB03/source-assertions/anti-stub-audit.txt`. |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative test |
| --- | --- | --- | --- | --- |
| N/A | SB03 introduces composition behavior, not a persisted production signal, state, record, or event. | N/A | N/A | N/A |
