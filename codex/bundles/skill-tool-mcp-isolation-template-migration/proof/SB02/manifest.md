# SB02 Proof Manifest

## Status

- Subbundle: `SB02`
- Status: `Completed`
- Validation depth: `Critical foundation`
- Owned requirements: R01, R02, R04, R07, R08, R09, R11, R12, R13, R14, R15
- Owned raw notes: internal class/service tools; external python/exe/http-style generic calls; explicit tool policy metadata; setup tests for external tools; mockable/testable loading and call mechanisms; preserve existing runtime tool names and policy semantics; structured external diagnostics.

## Semantic Contract

- `bundle://proof/SB02/semantic-invariants.md`

## Changed Files

- `bundle://proof/SB02/changed-file-hashes.txt`

## Command Transcripts

- Failing-first targeted tests: `bundle://proof/SB02/transcripts/failing-first-tool-implementation-contracts.txt`
- Passing targeted tests: `bundle://proof/SB02/transcripts/passing-tool-implementation-contracts.txt`
- Full build: `bundle://proof/SB02/transcripts/dotnet-build-solution.txt`
- Source assertions: `bundle://proof/SB02/transcripts/source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
- Static/performance scan: `bundle://proof/SB02/transcripts/static-performance-scan.txt`

## Failing-First Proof

- `bundle://proof/SB02/transcripts/failing-first-tool-implementation-contracts.txt`
- The transcript captures the focused SB02 contract tests failing to compile before the tool abstraction and implementation projects existed. That failure proves the test suite was introduced before the production tool layer.

## Passing Proof

- `bundle://proof/SB02/transcripts/passing-tool-implementation-contracts.txt`
- `bundle://proof/SB02/transcripts/dotnet-build-solution.txt`
- The passing transcript includes 9 targeted tests covering internal tool resolution, external process diagnostics, HTTP diagnostics, command rejection, timeout diagnostics, setup-test propagation, access-policy participation, and existing tool metadata parity.

## Source Assertions

- `repo://src/CanDoItAll.AgentFramework.Tools.Abstractions/Tools.cs`
- `repo://src/CanDoItAll.AgentFramework.Tools/Internal/InternalToolRegistry.cs`
- `repo://src/CanDoItAll.AgentFramework.Tools/Descriptors/ToolDescriptorFactory.cs`
- `repo://src/CanDoItAll.AgentFramework.Tools/Descriptors/ToolExposureDescriptorFactory.cs`
- `repo://src/CanDoItAll.AgentFramework.Tools/External/ExternalProcessToolInvoker.cs`
- `repo://src/CanDoItAll.AgentFramework.Tools/External/ExternalHttpToolInvoker.cs`
- `repo://src/CanDoItAll.AgentFramework.Tools/External/ToolDiagnostics.cs`
- `repo://src/CanDoItAll.AgentFramework.Tools/Setup/ToolSetupTestService.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ToolImplementationContractsTests.cs`
- Source assertion transcript: `bundle://proof/SB02/transcripts/source-assertions.txt`

## Anti-Stub Audit

- Command transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
- Result: no production `TODO`, `NotImplemented`, known shallow-stub return patterns, or fake markers under the SB02 tool projects.

## Browser Or Host Proof

- Browser proof: N/A. SB02 has no browser-visible surface.
- Host proof: external process and HTTP paths are exercised through deterministic injected transports in `ToolImplementationContractsTests`; live process/MCP host wiring remains later SB08/SB10/SB11 scope.

## Downstream Smoke Proof

- `bundle://proof/SB02/transcripts/dotnet-build-solution.txt` proves the tool abstraction and implementation projects compile inside `CanDoItAll.slnx`.
- `bundle://proof/SB02/transcripts/passing-tool-implementation-contracts.txt` proves the descriptors consume SB01 typed capability contracts and can participate in the shared access-policy evaluator before SB05/SB06 consume the foundation.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `ToolInvocationResult` | `repo://src/CanDoItAll.AgentFramework.Tools.Abstractions/Tools.cs`, `repo://src/CanDoItAll.AgentFramework.Tools/External/ExternalProcessToolInvoker.cs`, and `repo://src/CanDoItAll.AgentFramework.Tools/External/ExternalHttpToolInvoker.cs` define success/failure output and diagnostics. | `repo://tests/CanDoItAll.Tests.Unit/ToolImplementationContractsTests.cs` consumes the result in internal, process, HTTP, timeout, and schema-validation tests. | `bundle://proof/SB02/transcripts/passing-tool-implementation-contracts.txt` exercises internal registry invocation and external invocation lifecycle through production invokers. | `bundle://proof/SB02/transcripts/failing-first-tool-implementation-contracts.txt` shows no result contract existed before the tool abstraction projects were added. |
| `CapabilitySetupTestResult` | `repo://src/CanDoItAll.AgentFramework.Tools/Setup/ToolSetupTestService.cs` maps external invocation results into setup-test results without flattening diagnostics. | `repo://tests/CanDoItAll.Tests.Unit/ToolImplementationContractsTests.cs` validates schema failure propagation in `SB02_INV_EXTERNAL_005`. | `bundle://proof/SB02/transcripts/passing-tool-implementation-contracts.txt` runs setup testing through the production service. | `SB02_INV_EXTERNAL_005` rejects a shallow setup service that would convert schema failure into generic success or a generic error. |
| `CapabilityExposureDescriptor` | `repo://src/CanDoItAll.AgentFramework.Tools/Descriptors/ToolExposureDescriptorFactory.cs` maps internal, external, and provider-native tool descriptors into the shared SB01 access descriptor. | `repo://tests/CanDoItAll.Tests.Unit/ToolImplementationContractsTests.cs` evaluates descriptors through `CapabilityAccessPolicyEvaluator` in `SB02_INV_POLICY_001`. | `bundle://proof/SB02/transcripts/passing-tool-implementation-contracts.txt` proves descriptors carry typed key, runtime tool name, implementation key, tags, classifications, and side-effect profile into policy evaluation. | `SB02_INV_POLICY_001` denies internal mutation, external-tagged, and provider-native descriptors without adding tool-only suppression code. |
| `CapabilityDiagnostic` | `repo://src/CanDoItAll.AgentFramework.Tools/External/ToolDiagnostics.cs` emits typed category, transport, exit/status, timeout, correlation, masked detail, and repair hint. | `repo://tests/CanDoItAll.Tests.Unit/ToolImplementationContractsTests.cs` asserts `ProcessExit`, `HttpStatus`, `CommandPolicy`, `Timeout`, and `SchemaValidation` categories. | `bundle://proof/SB02/transcripts/static-performance-scan.txt` and passing tests prove diagnostics are bounded/masked and avoid blocking sync calls. | `SB02_INV_EXTERNAL_001`, `SB02_INV_EXTERNAL_002`, `SB02_INV_EXTERNAL_003`, `SB02_INV_EXTERNAL_004`, and `SB02_INV_EXTERNAL_006` cover non-zero exit, HTTP failure, disallowed command, and timeout negative paths. |
