# SB04 Proof Manifest

## Subbundle

- ID: SB04
- Title: Process tool migration into Processes module
- Status: Completed
- Critical foundation: Yes
- Owned requirements: RQ-004, RQ-005, RQ-006, RQ-007, RQ-009, RQ-011, RQ-014
- Raw notes: "rozplest ty zavislosti"; "po mensich krocich"; "nesmi veci zjednodusit nebo neco vynechat"
- Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.md`

## Changed Files With Hashes

| File | SHA-256 | Reason |
| --- | --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs` | `A4275F45136968DF881BDDAD80DE16F2E7B85CB43470F51A92F6B9288919B49F` | Moves process runtime tool construction and DTOs into the Processes module provider. |
| `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` | `B5287D20801941999167A55C71DE49A1F016D7771EE1FD390DA69C312AB3CC06` | Registers the process runtime tool provider through DI. |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs` | `E332CE73E17F5F36D61626DBA7E566E924E54A293A84B03DD837ADFF74524539` | Uses the old internal process builder only as a compatibility fallback when no registered providers exist. |
| `repo://tests/CanDoItAll.Tests.Integration/MafAgentRuntimeTests.cs` | `874C8DB057A8BDF4B603BDF325531FBBA6DB21B8FAE11BE6A6C4F49AD3E25336` | Adds provider-path parity and access-denial coverage. |
| Changed file hash transcript | `bundle://proof/SB04/source-assertions/changed-file-hashes.txt` | Full hash evidence for touched SB04 source/test files. |

## Commands

| Command | Transcript path | Exit code | Purpose |
| --- | --- | ---: | --- |
| `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessAgentRuntimeToolProviderParity"` | `bundle://proof/SB04/transcripts/process-agent-runtime-tool-provider-parity-test.txt` | 0 | Proves the registered provider attaches the exact 23 process tool names and preserves approval wrapping classification. |
| `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessAgentRuntimeToolProviderAccess"` | `bundle://proof/SB04/transcripts/process-agent-runtime-tool-provider-access-test.txt` | 0 | Proves provider tools enforce read, write, and allowed-definition scope checks. |
| `dotnet build CanDoItAll.slnx` | `bundle://proof/SB04/transcripts/solution-build.txt` | 0 | Proves the full solution builds after provider migration. |

## Validator Proof Citations

- Adversarial negative proof: N/A process/non-production preserved failing-first transcript; provider parity and access-denial tests are the maintained regression proof.
- Passing transcript: `bundle://proof/SB04/transcripts/process-agent-runtime-tool-provider-parity-test.txt`.
- Anti-stub audit transcript: `bundle://proof/SB04/transcripts/anti-stub-audit.txt`.

## Source Assertions

| Assertion | Source path | Result |
| --- | --- | --- |
| Process provider exposes exactly the prepared 23-name inventory. | `bundle://proof/SB04/source-assertions/tool-parity-source-assertion.txt` | Expected count 23, actual count 23, no missing or unexpected names. |
| Processes module registers the provider through `TryAddEnumerable`. | `bundle://proof/SB04/source-assertions/provider-registration-source-assertion.txt` | `ProcessAgentRuntimeToolProvider` registration and Tooling namespace usage exist. |
| MAF old process path is no longer used when providers are registered. | `bundle://proof/SB04/source-assertions/maf-provider-fallback-source-assertion.txt` | `CreateProcessToolBuilder()` is gated behind `runtimeToolProviders.Count == 0`. |
| Dispatcher unchanged. | `bundle://proof/SB04/source-assertions/dispatcher-unchanged.txt` | No git diff under process dispatcher paths. |
| Anti-stub audit passed. | `bundle://proof/SB04/source-assertions/anti-stub-audit.txt` | No TODO, `NotImplemented`, or placeholder matches in SB04 source/test changes. |

## Semantic Adequacy Gate

| Label | Evidence |
| --- | --- |
| Raw note owned | Decouples process runtime tools from MAF in a small migration step without simplifying tool behavior. |
| Shipped behavior | Processes owns `ProcessAgentRuntimeToolProvider`; MAF attaches process tools through registered runtime providers when the Processes module is present. |
| Source proof | `bundle://proof/SB04/source-assertions/tool-parity-source-assertion.txt`, `provider-registration-source-assertion.txt`, and `maf-provider-fallback-source-assertion.txt`. |
| Test proof | `bundle://proof/SB04/transcripts/process-agent-runtime-tool-provider-parity-test.txt`, `process-agent-runtime-tool-provider-access-test.txt`, and `solution-build.txt`. |
| Shallow-pass trap | A count-only or registration-only implementation would miss exact-name parity or access behavior; the parity and access tests invoke the provider path explicitly. |
| Adversarial negative proof | Read-disabled, write-disabled, and definition-scope-denied agents throw explicit provider tool errors instead of silently allowing operations. |
| Semantic positive proof | The registered provider contributes all 23 tools; MAF wraps mutation tools and leaves read tools approval-free through the same policy path. |
| Anti-stub audit | `bundle://proof/SB04/source-assertions/anti-stub-audit.txt`. |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative test |
| --- | --- | --- | --- | --- |
| N/A | SB04 moves runtime tool construction and DI wiring; it introduces no persisted production state, signal, record, or event. | N/A | N/A | N/A |
