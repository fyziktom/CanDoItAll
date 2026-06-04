# SB02 Proof Manifest

## Subbundle

- ID: SB02
- Title: Agent runtime tooling abstractions
- Status: Completed
- Critical foundation: Yes
- Owned requirements: RQ-003, RQ-014
- Raw notes: "rozplest ty zavislosti"; "po mensich krocich"; "nesmi veci zjednodusit nebo neco vynechat"
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`

## Changed Files With Hashes

| File | SHA-256 | Reason |
| --- | --- | --- |
| `repo://CanDoItAll.slnx` | `601747BB49043C5120FC69CC1485F16D58B023E6F471BCB168FB9154A9B7DB0C` | Adds Tooling project to solution. |
| `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `445C93690798746E04A2C2AC937D6C17BEB62CB3CF6E787875C5501647576A1E` | MAF references provider-neutral Tooling while retaining temporary Processes reference. |
| `repo://src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj` | `CA1FA9F74A7DB3FE461F1E3544E48286B01D55DD09E5D20CD35FED7262BA36E8` | Processes can reference provider-neutral Tooling for SB04 migration. |
| `repo://src/CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj` | `88EC273AF9F37484EC73992AEDDA353C56604B25E9026207CCFCEE3A9AC8EDFA` | New abstraction project. |
| `repo://src/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderContext.cs` | `C40F44BC06B8BFF4B2A66CAA6A72B7C4BCEB40B5F622A86547F68E12A1F087A8` | Provider-neutral runtime context contract. |
| `repo://src/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderPurpose.cs` | `0D6882EFBE3F8FF9209F1A58E7A69F199B3BF0283C0506157619CD4268A12172` | Provider purpose enum. |
| `repo://src/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs` | `03FC4162C891352D81A308C2FE829CDFC421ECCA41893864EA60468BD169CE77` | Runtime tool provider abstraction. |
| `repo://tests/CanDoItAll.Tests.Unit/AgentRuntimeToolProviderArchitectureTests.cs` | `6E7D017A756A7DE33DB4C872B234212213D7CAEE02BF4C0EAC82AC84641E0632` | Static architecture guard tests. |
| Before/after hash transcript | `bundle://proof/SB02/source-assertions/changed-file-hashes.txt` | Full before/after hash evidence for touched files. |

## Commands

| Command | Transcript path | Exit code | Purpose |
| --- | --- | ---: | --- |
| `dotnet build src\CanDoItAll.AgentFramework.Tooling\CanDoItAll.AgentFramework.Tooling.csproj` | `bundle://proof/SB02/transcripts/tooling-project-build.txt` | 0 | Proves new abstraction project builds. |
| `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~AgentRuntimeToolProviderArchitectureTests"` | `bundle://proof/SB02/transcripts/architecture-tests.txt` | 0 | Proves Tooling dependency guard and MAF/Processes references. |
| `dotnet build CanDoItAll.slnx` | `bundle://proof/SB02/transcripts/solution-build.txt` | 0 | Proves solution builds after adding project and references. |

## Validator Proof Citations

- Adversarial negative proof: N/A process/non-production preserved failing-first transcript; the architecture test transcript is the maintained regression proof for invalid dependency direction.
- Passing transcript: `bundle://proof/SB02/transcripts/architecture-tests.txt`.
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`.

## Source Assertions

| Assertion | Source path | Result |
| --- | --- | --- |
| Tooling project does not reference product modules. | `bundle://proof/SB02/source-assertions/tooling-project-reference-audit.txt`; `repo://src/CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj` | Tooling references only Models and `Microsoft.Extensions.AI.Abstractions`; module reference flag is false. |
| Tooling source is provider-neutral. | `bundle://proof/SB02/source-assertions/tooling-source-forbidden-namespace-audit.txt`; `repo://src/CanDoItAll.AgentFramework.Tooling` | No `CanDoItAll.Modules.*`, `ProcessesService`, or `ProcessTool` references. |
| MAF and Processes can reference Tooling during compatibility phase. | `bundle://proof/SB02/source-assertions/tooling-project-reference-audit.txt` | Both project files contain Tooling project references. |
| No process tools moved yet. | `bundle://proof/SB02/source-assertions/no-process-tool-migration-yet.txt` | Current MAF process builder references remain for later SB03/SB04/SB05. |
| Dispatcher unchanged. | `bundle://proof/SB02/source-assertions/dispatcher-unchanged.txt` | No dispatcher files in diff. |
| Anti-stub audit passed. | `bundle://proof/SB02/source-assertions/anti-stub-audit.txt` | No `TODO`, `NotImplemented`, or `NotImplementedException` stubs in SB02 source/test. |

## Semantic Adequacy Gate

| Label | Evidence |
| --- | --- |
| Raw note owned | Decouple MAF from Processes in small safe steps without simplifying or omitting runtime process behavior. |
| Shipped behavior | Added a provider-neutral `CanDoItAll.AgentFramework.Tooling` project with runtime tool-provider contracts and wired MAF/Processes to reference it without moving process tools yet. |
| Source proof | `repo://src/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs`, MAF/Processes csproj references, and `bundle://proof/SB02/source-assertions/tooling-project-reference-audit.txt`. |
| Test proof | `bundle://proof/SB02/transcripts/tooling-project-build.txt`, `bundle://proof/SB02/transcripts/architecture-tests.txt`, and `bundle://proof/SB02/transcripts/solution-build.txt`. |
| Shallow-pass trap | Adding an interface inside MAF or Processes would compile but keep the dependency direction wrong. |
| Adversarial negative proof | `AgentRuntimeToolProviderArchitectureTests.Tooling_project_does_not_reference_product_modules` rejects any `CanDoItAll.Modules.*` project reference in Tooling. |
| Semantic positive proof | The Tooling project builds, is in the solution, exposes provider-neutral contracts, and is referenced by both MAF and Processes. |
| Anti-stub audit | `bundle://proof/SB02/source-assertions/anti-stub-audit.txt`. |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative test |
| --- | --- | --- | --- | --- |
| N/A | SB02 introduces contracts only, not a production signal, state, record, or event. | N/A | N/A | N/A |
