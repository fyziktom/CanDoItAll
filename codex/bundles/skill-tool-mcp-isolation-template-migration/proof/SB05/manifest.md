# SB05 Proof Manifest

## Status

- Subbundle: `SB05`
- Status: `Completed`
- Validation depth: `Mandatory hardening checkpoint`
- Owned requirements: R01, R04, R05, R07, R08, R09, R11, R12, R13, R14, R15
- Owned raw notes: harden isolated capability services before template loading and MAF reconnection; keep dependencies away from MAF/UI; preserve typed access policy behavior; verify structured diagnostics, cancellation, cleanup, file size, and performance guardrails.

## Semantic Contract

- `bundle://proof/SB05/semantic-invariants.md`

## Changed Files

- `bundle://proof/SB05/changed-file-hashes.txt`

## Command Transcripts

- Failing-first targeted tests: `bundle://proof/SB05/transcripts/failing-first-capability-hardening-tests.txt`
- Passing targeted tests: `bundle://proof/SB05/transcripts/passing-capability-hardening-tests.txt`
- Existing tool regression tests: `bundle://proof/SB05/transcripts/regression-tool-implementation-contracts.txt`
- Full build: `bundle://proof/SB05/transcripts/dotnet-build-solution.txt`
- Source assertions: `bundle://proof/SB05/transcripts/source-assertions.txt`
- Dependency-direction scan: `bundle://proof/SB05/transcripts/dependency-direction-scan.txt`
- Anti-stub audit: `bundle://proof/SB05/transcripts/anti-stub-audit.txt`
- Static/performance scan: `bundle://proof/SB05/transcripts/static-performance-scan.txt`
- File-size scan: `bundle://proof/SB05/transcripts/file-size-scan.txt`

## Failing-First Proof

- `bundle://proof/SB05/transcripts/failing-first-capability-hardening-tests.txt`
- The failing transcript captures `SB05_INV_DIAGNOSTICS_004` exposing `raw-secret-value` from direct `Authorization=Bearer ...` exception detail in external HTTP tool diagnostics. This proved a real diagnostics hardening gap before the production fix.

## Passing Proof

- `bundle://proof/SB05/transcripts/passing-capability-hardening-tests.txt`
- `bundle://proof/SB05/transcripts/regression-tool-implementation-contracts.txt`
- `bundle://proof/SB05/transcripts/dotnet-build-solution.txt`
- The passing transcript includes 7 SB05 tests for external HTTP masking and typed diagnostic shape, process cancellation, HTTP cancellation, direct bearer-assignment masking, deny/require policy precedence, future capability suppression by tag, and common exposure metadata shape.
- The tool regression transcript keeps the 9 SB02 tool implementation contracts passing after the diagnostic masking fix.
- The solution build passed with 0 warnings and 0 errors.

## Source Assertions

- `repo://src/CanDoItAll.AgentFramework.Tools/External/ToolDiagnostics.cs`
- `repo://src/CanDoItAll.AgentFramework.Capabilities.Abstractions/CapabilityEnums.cs`
- `repo://src/CanDoItAll.AgentFramework.Capabilities.Abstractions/CapabilityIdentifiers.cs`
- `repo://src/CanDoItAll.AgentFramework.Capabilities.Abstractions/CapabilityModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Capabilities.Abstractions/CapabilityNameRules.cs`
- `repo://src/CanDoItAll.AgentFramework.Capabilities.Abstractions/CapabilityText.cs`
- `repo://src/CanDoItAll.AgentFramework.Capabilities.Templates/CapabilityTemplateDtos.cs`
- `repo://src/CanDoItAll.AgentFramework.Capabilities.Templates/CapabilityTemplateValidator.cs`
- `repo://src/CanDoItAll.AgentFramework.Capabilities.Templates/CapabilityAccessPolicyTemplateCompiler.cs`
- `repo://tests/CanDoItAll.Tests.Unit/CapabilityFoundationHardeningTests.cs`
- Source assertion transcript: `bundle://proof/SB05/transcripts/source-assertions.txt`

## Hardening Results

| Area | Result | Proof |
|---|---|---|
| Dependency direction | No MAF, Blazor/UI, Radzen, `Microsoft.Agents`, or `ModelContextProtocol` references in isolated capability projects. | `bundle://proof/SB05/transcripts/dependency-direction-scan.txt` |
| Diagnostics | Tool bearer-token masking now handles direct `Authorization=Bearer ...` detail before generic assignment masking. | `SB05_INV_DIAGNOSTICS_004`, `repo://src/CanDoItAll.AgentFramework.Tools/External/ToolDiagnostics.cs` |
| Access policy | Deny over allow, required denied diagnostics, missing require-rule diagnostics, and future capability kind tag suppression are covered. | `SB05_INV_POLICY_001`, `SB05_INV_POLICY_002` |
| Exposure metadata | Tool, skill, MCP server, and MCP child tool descriptors share policy metadata shape. | `SB05_INV_EXPOSURE_001` |
| File size | `Capabilities.cs` and `CapabilityTemplateModels.cs` were split; all capability foundation files are under 500 lines. | `bundle://proof/SB05/transcripts/file-size-scan.txt` |
| Performance | No sync-over-async, blocking read, ad hoc serializer-options, reflection, or service-locator matches in the isolated projects. | `bundle://proof/SB05/transcripts/static-performance-scan.txt` |
| Stubs | No disallowed `TODO`, `NotImplemented`, shallow return stubs, or fake markers outside the intentional MCP fake fixture. | `bundle://proof/SB05/transcripts/anti-stub-audit.txt` |

## Accepted Risks

- None. The overgrown-file findings were fixed by splitting the generated foundation files instead of deferring them.

## Browser Or Host Proof

- Browser proof: N/A. SB05 has no browser-visible surface; SB10/SB11 will carry large-screen-only UI proof per user instruction.
- Host proof: targeted unit/regression tests and static scans cover this checkpoint.

## Downstream Smoke Proof

- `bundle://proof/SB05/transcripts/dotnet-build-solution.txt` proves the split foundation files and masking fix compile across `CanDoItAll.slnx`.
- `bundle://proof/SB05/transcripts/passing-capability-hardening-tests.txt` proves SB06 can consume the hardened foundation without depending on MAF/UI or happy-path-only diagnostics.
