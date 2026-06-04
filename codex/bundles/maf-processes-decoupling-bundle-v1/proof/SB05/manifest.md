# SB05 Proof Manifest

## Subbundle

- ID: SB05
- Title: Remove MAF -> Processes project reference
- Status: Completed
- Critical foundation: Yes
- Owned requirements: RQ-001, RQ-002, RQ-008, RQ-010, RQ-011, RQ-014
- Raw notes: "rozplest ty zavislosti"; "po mensich krocich"; "nesmi veci zjednodusit nebo neco vynechat"
- Semantic invariant contract: `bundle://proof/SB05/semantic-invariants.md`

## Changed Files With Hashes

| File | SHA-256 | Reason |
| --- | --- | --- |
| `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `E382A1B9D09400DA749997958F24FF3F6BA8B987D1CAC376E814D2DF307A3E42` | Removes the direct Processes project reference. |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs` | `74EE50BF14DFD9B880A7D7E79CE3B3E8C2FB41A29EF3244326C5258793F472C3` | Removes the legacy process builder slot and attachment path. |
| `repo://src/CanDoItAll.AgentFramework.Maf/README.md` | `9CF5C1DF5B265F7E47A44C7EC84FEC5BC8FC86FA08CA72A9D39AA764E626DDCD` | Documents provider-based process tool composition. |
| `repo://tests/CanDoItAll.Tests.Unit/AgentRuntimeHardeningStaticRegressionTests.cs` | `23B38F06B148B01C33EE697101C9CB3A679BFA87C274FB065A72A2876604D2C4` | Adds static architecture guard for forbidden direct MAF Processes dependency. |
| `src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProcessTools.cs` | `DELETED` | Removes obsolete legacy process tool builder implementation. |
| Changed file hash transcript | `bundle://proof/SB05/source-assertions/changed-file-hashes.txt` | Full hash evidence for touched SB05 source/test/doc files. |

## Commands

| Command | Transcript path | Exit code | Purpose |
| --- | --- | ---: | --- |
| `rg -n "CanDoItAll\.Modules\.Processes|ProcessToolBuilder|CreateProcessToolBuilder|MafAgentRuntime\.ProcessTools" src\CanDoItAll.AgentFramework.Maf` | `bundle://proof/SB05/transcripts/maf-forbidden-processes-scan.txt` | 0 | Proves forbidden MAF source/project strings have no matches; no-match `rg` exit 1 was normalized to proof success. |
| `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~Maf_runtime_has_no_compile_time_processes_module_dependency"` | `bundle://proof/SB05/transcripts/static-architecture-test.txt` | 0 | Proves direct dependency guard passes. |
| `dotnet build src\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj` | `bundle://proof/SB05/transcripts/maf-project-build.txt` | 0 | Proves MAF builds after reference removal and legacy partial deletion. |
| `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessAgentRuntimeToolProviderParity"` | `bundle://proof/SB05/transcripts/process-provider-parity-after-reference-removal.txt` | 0 | Proves process provider still supplies all process tools through app composition after removing the legacy MAF path. |
| `dotnet build CanDoItAll.slnx` | `bundle://proof/SB05/transcripts/solution-build.txt` | 0 | Proves full solution builds. |

## Validator Proof Citations

- Adversarial negative proof transcript: `bundle://proof/SB05/transcripts/maf-forbidden-processes-scan.txt`.
- Passing transcript: `bundle://proof/SB05/transcripts/static-architecture-test.txt`.
- Anti-stub audit transcript: `bundle://proof/SB05/transcripts/anti-stub-audit.txt`.

## Source Assertions

| Assertion | Source path | Result |
| --- | --- | --- |
| MAF project has no direct Processes project reference. | `bundle://proof/SB05/source-assertions/maf-project-reference-audit.txt` | No `CanDoItAll.Modules.Processes` reference in MAF csproj. |
| MAF source/docs have no forbidden process dependency strings. | `bundle://proof/SB05/source-assertions/maf-forbidden-source-audit.txt` | No `CanDoItAll.Modules.Processes`, `ProcessToolBuilder`, `CreateProcessToolBuilder`, or `MafAgentRuntime.ProcessTools` matches under MAF. |
| Legacy process tool file deleted. | `bundle://proof/SB05/source-assertions/legacy-process-tool-file-deleted.txt` | `MafAgentRuntime.ProcessTools.cs` does not exist. |
| Provider composition remains in MAF. | `bundle://proof/SB05/source-assertions/composition-cleanup-source-audit.txt` | MAF still resolves `IAgentRuntimeToolProvider` and attaches registered providers. |
| Anti-stub audit passed. | `bundle://proof/SB05/source-assertions/anti-stub-audit.txt` | No TODO, `NotImplemented`, stub, or placeholder matches in SB05 source/test/doc changes. |

## Semantic Adequacy Gate

| Label | Evidence |
| --- | --- |
| Raw note owned | Removes the direct dependency after SB04 proved provider migration, preserving the small-step sequence. |
| Shipped behavior | MAF no longer owns process tool construction or references the Processes module directly; process tools still arrive via registered providers. |
| Source proof | `bundle://proof/SB05/source-assertions/maf-project-reference-audit.txt`, `maf-forbidden-source-audit.txt`, `legacy-process-tool-file-deleted.txt`, and `composition-cleanup-source-audit.txt`. |
| Test proof | `bundle://proof/SB05/transcripts/static-architecture-test.txt`, `bundle://proof/SB05/transcripts/maf-project-build.txt`, `bundle://proof/SB05/transcripts/process-provider-parity-after-reference-removal.txt`, and `bundle://proof/SB05/transcripts/solution-build.txt`. |
| Shallow-pass trap | Simply deleting the project reference would pass a source scan but break process tool availability; the post-removal parity integration test proves provider composition still supplies tools. |
| Adversarial negative proof | The static guard fails if MAF source/docs/project files reintroduce the Processes namespace, the legacy process builder, or the deleted partial name. |
| Semantic positive proof | MAF builds without the direct Processes reference, the full solution builds, and the provider path still exposes all process tools. |
| Anti-stub audit | `bundle://proof/SB05/source-assertions/anti-stub-audit.txt`. |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative test |
| --- | --- | --- | --- | --- |
| N/A | SB05 removes a compile-time dependency and legacy source path; it introduces no persisted production state, signal, record, or event. | N/A | N/A | N/A |
