# SB05 Semantic Invariants

## SB05_INV_DIAGNOSTICS_001

- Source raw note: external tool diagnostics must include typed category, key/kind, transport, bounded masked detail, correlation ID, and repair hint.
- Expected behavior: external HTTP status diagnostics preserve `HttpStatus`, capability kind/key, `ExternalHttp` transport, status code, field path, bounded detail, correlation ID, and repair hint while masking bearer/header/body secrets.
- Disallowed shallow implementation: return generic HTTP failure text or leak secret-bearing headers/body content.
- Failing-first proof: `bundle://proof/SB05/transcripts/failing-first-capability-hardening-tests.txt`
- Passing proof: `bundle://proof/SB05/transcripts/passing-capability-hardening-tests.txt`
- Changed source files and hashes: `bundle://proof/SB05/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Tools/External/ToolDiagnostics.cs`, `repo://src/CanDoItAll.AgentFramework.Tools/External/ExternalHttpToolInvoker.cs`, `bundle://proof/SB05/transcripts/source-assertions.txt`
- Red-team negative case: status response includes authorization header and raw secret payload; diagnostic must not contain `raw-secret-value`.
- Downstream dependency check: SB10 can surface setup/call failures without leaking credential material.

## SB05_INV_DIAGNOSTICS_002

- Source raw note: external process cancellation must be explicit and typed.
- Expected behavior: process cancellation returns `Cancellation`, `Tool` kind/key, `ExternalProcess` transport, descriptor timeout, correlation ID, masked detail, and retry guidance.
- Disallowed shallow implementation: let `OperationCanceledException` bubble out or report it as process start failure.
- Failing-first proof: `bundle://proof/SB05/transcripts/failing-first-capability-hardening-tests.txt`
- Passing proof: `bundle://proof/SB05/transcripts/passing-capability-hardening-tests.txt`
- Changed source files and hashes: `bundle://proof/SB05/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Tools/External/ExternalProcessToolInvoker.cs`, `bundle://proof/SB05/transcripts/source-assertions.txt`
- Red-team negative case: fake process runner throws `OperationCanceledException`.
- Downstream dependency check: SB08/SB11 can distinguish cancellation from failed external tool configuration.

## SB05_INV_DIAGNOSTICS_003

- Source raw note: external HTTP cancellation must be explicit and typed.
- Expected behavior: HTTP cancellation returns `Cancellation`, `Tool` kind/key, `ExternalHttp` transport, descriptor timeout, and correlation ID.
- Disallowed shallow implementation: treat cancellation as HTTP status failure or success.
- Failing-first proof: `bundle://proof/SB05/transcripts/failing-first-capability-hardening-tests.txt`
- Passing proof: `bundle://proof/SB05/transcripts/passing-capability-hardening-tests.txt`
- Changed source files and hashes: `bundle://proof/SB05/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Tools/External/ExternalHttpToolInvoker.cs`, `bundle://proof/SB05/transcripts/source-assertions.txt`
- Red-team negative case: fake HTTP transport throws `OperationCanceledException`.
- Downstream dependency check: SB10 setup/API can report HTTP cancellation separately from endpoint failures.

## SB05_INV_DIAGNOSTICS_004

- Source raw note: diagnostics must mask sensitive data even when transport errors include raw authorization detail directly.
- Expected behavior: direct exception detail containing `Authorization=Bearer raw-secret-value` is masked before generic assignment masking can leave the raw token suffix behind.
- Disallowed shallow implementation: mask only formatted headers and miss arbitrary exception detail.
- Failing-first proof: `bundle://proof/SB05/transcripts/failing-first-capability-hardening-tests.txt`
- Passing proof: `bundle://proof/SB05/transcripts/passing-capability-hardening-tests.txt`
- Changed source files and hashes: `bundle://proof/SB05/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Tools/External/ToolDiagnostics.cs`, `bundle://proof/SB05/transcripts/source-assertions.txt`
- Red-team negative case: failing-first transcript shows `Authorization=*** raw-secret-value` before the fix.
- Downstream dependency check: template/runtime setup can include raw exception detail without credential leakage.

## SB05_INV_POLICY_001

- Source raw note: access policy evaluator precedence must be deterministic before templates and MAF consume it.
- Expected behavior: deny wins over allow, denied required candidates emit `RequiredCapabilityDenied`, and unmatched require rules emit explicit missing-required diagnostics.
- Disallowed shallow implementation: let allow grant denied capabilities or silently ignore require rules that match no assigned candidate.
- Failing-first proof: `bundle://proof/SB05/transcripts/failing-first-capability-hardening-tests.txt`
- Passing proof: `bundle://proof/SB05/transcripts/passing-capability-hardening-tests.txt`
- Changed source files and hashes: `bundle://proof/SB05/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Capabilities.Access/CapabilityAccessPolicyEvaluator.cs`, `bundle://proof/SB05/transcripts/source-assertions.txt`
- Red-team negative case: one policy both allows and denies mutation while also requiring a missing future tag.
- Downstream dependency check: SB06/SB08 can consume policy templates without reimplementing precedence.

## SB05_INV_POLICY_002

- Source raw note: future capability descriptors must participate in generic suppression without evaluator code changes.
- Expected behavior: a `Memory` capability descriptor is suppressed by tag through the same evaluator path used by tools, skills, and MCPs.
- Disallowed shallow implementation: hard-code evaluator matching to only current tool/skill/MCP descriptor families.
- Failing-first proof: `bundle://proof/SB05/transcripts/failing-first-capability-hardening-tests.txt`
- Passing proof: `bundle://proof/SB05/transcripts/passing-capability-hardening-tests.txt`
- Changed source files and hashes: `bundle://proof/SB05/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Capabilities.Access/CapabilityAccessPolicyEvaluator.cs`, `bundle://proof/SB05/transcripts/source-assertions.txt`
- Red-team negative case: a future `Memory` descriptor with `external` tag is denied by tag selector.
- Downstream dependency check: later capability kinds can join the same policy model without evaluator surgery.

## SB05_INV_EXPOSURE_001

- Source raw note: every capability kind must expose common policy metadata.
- Expected behavior: tool, skill, MCP server, and MCP child tool descriptors all expose identity, display name, tags, operation classifications, side-effect profile, availability/source metadata, and server/tool context where applicable.
- Disallowed shallow implementation: let each capability family publish incompatible metadata or opaque string selectors.
- Failing-first proof: `bundle://proof/SB05/transcripts/failing-first-capability-hardening-tests.txt`
- Passing proof: `bundle://proof/SB05/transcripts/passing-capability-hardening-tests.txt`
- Changed source files and hashes: `bundle://proof/SB05/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Tools/Descriptors/ToolExposureDescriptorFactory.cs`, `repo://src/CanDoItAll.AgentFramework.Skills/Descriptors/SkillExposureDescriptorFactory.cs`, `repo://src/CanDoItAll.AgentFramework.Mcp/Descriptors/McpExposureDescriptorFactory.cs`, `bundle://proof/SB05/transcripts/source-assertions.txt`
- Red-team negative case: MCP child tool assertion requires both server key and tool name, preventing opaque tool-only suppression.
- Downstream dependency check: SB08/SB11 can evaluate one effective capability set across all families.

## SB05_INV_STATIC_001

- Source raw note: hardening checkpoint must block dependency direction, file size, performance, and stub regressions before template materialization.
- Expected behavior: isolated capability projects have no MAF/UI dependencies, no focused performance anti-pattern matches, no disallowed stubs, and no files over 500 lines.
- Disallowed shallow implementation: document overgrown files or coupling as accepted risk when the split is straightforward.
- Failing-first proof: `bundle://proof/SB05/transcripts/file-size-scan.txt` initially found overgrown files before the split.
- Passing proof: `bundle://proof/SB05/transcripts/dependency-direction-scan.txt`, `bundle://proof/SB05/transcripts/static-performance-scan.txt`, `bundle://proof/SB05/transcripts/anti-stub-audit.txt`, `bundle://proof/SB05/transcripts/file-size-scan.txt`
- Changed source files and hashes: `bundle://proof/SB05/changed-file-hashes.txt`
- Production assertions: split files under `repo://src/CanDoItAll.AgentFramework.Capabilities.Abstractions/` and `repo://src/CanDoItAll.AgentFramework.Capabilities.Templates/`.
- Red-team negative case: the file-size scanner fails the command if any isolated capability file exceeds 500 lines.
- Downstream dependency check: SB06 can build template materialization over focused files and stable dependency direction.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `CapabilityDiagnostic` | `repo://src/CanDoItAll.AgentFramework.Tools/External/ToolDiagnostics.cs` | `repo://tests/CanDoItAll.Tests.Unit/CapabilityFoundationHardeningTests.cs` | `bundle://proof/SB05/transcripts/passing-capability-hardening-tests.txt` | `SB05_INV_DIAGNOSTICS_001`, `SB05_INV_DIAGNOSTICS_004` |
| `CapabilityAccessPolicyEvaluator` | `repo://src/CanDoItAll.AgentFramework.Capabilities.Access/CapabilityAccessPolicyEvaluator.cs` | `repo://tests/CanDoItAll.Tests.Unit/CapabilityFoundationHardeningTests.cs` | `SB05_INV_POLICY_001`, `SB05_INV_POLICY_002` | deny over allow, denied required, missing require rule, future kind by tag |
| `CapabilityExposureDescriptor` | Tool, skill, and MCP exposure factories | `repo://tests/CanDoItAll.Tests.Unit/CapabilityFoundationHardeningTests.cs` | `SB05_INV_EXPOSURE_001` | MCP child tool requires server-scoped tool metadata |
| Split foundation files | `CapabilityEnums.cs`, `CapabilityIdentifiers.cs`, `CapabilityModels.cs`, `CapabilityNameRules.cs`, `CapabilityText.cs`, `CapabilityTemplateDtos.cs`, `CapabilityTemplateValidator.cs`, `CapabilityAccessPolicyTemplateCompiler.cs` | Full solution build and existing SB01-SB04 tests compile against unchanged public types. | `bundle://proof/SB05/transcripts/dotnet-build-solution.txt` | `bundle://proof/SB05/transcripts/file-size-scan.txt` |

## Anti-Stub Audit

- `bundle://proof/SB05/transcripts/anti-stub-audit.txt`
