# SB01 Semantic Invariants

## SB01_INV_NAMES_001

- Source raw note: "If there are some standards about naming in case of tools skills and MCPs in general AI world, we must use proper naming conventions to assure compatibility..."
- Expected behavior: existing runtime tool names, agent capability keys, and process operation texts pass typed compatibility validators; invalid capability keys and runtime names fail before materialization.
- Disallowed shallow implementation: accept any non-empty string or infer runtime names from capability keys at execution time.
- Failing-first proof: supporting failure transcript `bundle://proof/SB01/transcripts/failing-first-capability-contracts-semantic.txt` captured before the full implementation; naming tests already passed because validators were implemented before access/template logic.
- Passing proof: `bundle://proof/SB01/transcripts/passing-capability-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB01/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Capabilities.Abstractions/Capabilities.cs`, `bundle://proof/SB01/transcripts/source-assertions.txt`
- Red-team negative case: invalid names with spaces, PascalCase runtime names, kebab-case runtime names, and snake_case capability keys fail in `SB01_INV_NAMES_002` and `SB01_INV_NAMES_003`.
- Downstream dependency check: SB02-SB04 can use typed IDs and validators without MAF private string switches.

## SB01_INV_TEMPLATE_001

- Source raw note: "External Tools and MCPs must have way how user can test them during setup" and "Preserve security posture. Raw secrets are rejected..."
- Expected behavior: template validation reports template path, capability key, field path, category, and repair hint for raw environment/header values and other invalid fields.
- Disallowed shallow implementation: return success for invalid templates or collapse validation into generic messages.
- Failing-first proof: `bundle://proof/SB01/transcripts/failing-first-capability-contracts-semantic.txt`
- Passing proof: `bundle://proof/SB01/transcripts/passing-capability-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB01/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Capabilities.Templates/CapabilityTemplateModels.cs`, `bundle://proof/SB01/transcripts/source-assertions.txt`
- Red-team negative case: `SB01_INV_TEMPLATE_001` provides raw `API_KEY` and `Authorization` values and requires typed `SecretBinding` diagnostics.
- Downstream dependency check: SB06/SB10 can reuse the validator/compiler and cannot silently seed or save invalid capability templates.

## SB01_INV_ACCESS_001

- Source raw note: "Limit/forbid tools, skills, MCPs by agent/process/workflow/UI without stringly code" and "allow does not grant capabilities that are not already assigned/enabled."
- Expected behavior: access evaluation starts from candidates, deny beats allow, and allow does not add missing capability assignments.
- Disallowed shallow implementation: allow-all candidate return, last-rule-wins behavior, or allow rules granting missing assignments.
- Failing-first proof: `bundle://proof/SB01/transcripts/failing-first-capability-contracts-semantic.txt`
- Passing proof: `bundle://proof/SB01/transcripts/passing-capability-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB01/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Capabilities.Access/CapabilityAccessPolicyEvaluator.cs`, `bundle://proof/SB01/transcripts/source-assertions.txt`
- Red-team negative case: `SB01_INV_ACCESS_001` defines an allow rule for a missing delete tool and a deny rule for mutation candidates; only the validation tool remains allowed.
- Downstream dependency check: SB08 can consume `EffectiveCapabilitySet` without reapplying hidden MAF filters.

## SB01_INV_ACCESS_002

- Source raw note: required capabilities must fail predictably when denied by policy and produce actionable diagnostics.
- Expected behavior: a required capability denied by policy is removed from allowed capabilities and emits `RequiredCapabilityDenied` with a repair hint.
- Disallowed shallow implementation: suppress the capability without explaining the required-capability conflict, or leave required capabilities attached despite deny rules.
- Failing-first proof: `bundle://proof/SB01/transcripts/failing-first-capability-contracts-semantic.txt`
- Passing proof: `bundle://proof/SB01/transcripts/passing-capability-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB01/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Capabilities.Access/CapabilityAccessPolicyEvaluator.cs`, `bundle://proof/SB01/transcripts/source-assertions.txt`
- Red-team negative case: `SB01_INV_ACCESS_002` denies all skills while declaring `aspnet-core-skill` required.
- Downstream dependency check: process/workflow dispatch can block with a typed diagnostic before runtime calls.

## SB01_INV_ACCESS_003

- Source raw note: new skill/tool/MCP implementations must participate in generic restrictions through common descriptors, without suppressor code changes.
- Expected behavior: a newly constructed MCP descriptor with tag `external` is suppressed by a tag policy through the same evaluator.
- Disallowed shallow implementation: hardcode known capability keys/kinds or ignore tags for new descriptors.
- Failing-first proof: `bundle://proof/SB01/transcripts/failing-first-capability-contracts-semantic.txt`
- Passing proof: `bundle://proof/SB01/transcripts/passing-capability-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB01/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Capabilities.Access/CapabilityAccessPolicyEvaluator.cs`, `bundle://proof/SB01/transcripts/source-assertions.txt`
- Red-team negative case: `SB01_INV_ACCESS_003` creates an MCP descriptor that was not known to the evaluator code and proves tag suppression still applies.
- Downstream dependency check: SB04 and SB08 can add MCP servers/tools without adding new hidden suppression switches.

## SB01_INV_POLICY_001

- Source raw note: template/UI strings must be parsed once into typed value objects and invalid selectors must fail before materialization.
- Expected behavior: duplicate rule IDs and invalid selector values produce `AccessPolicy` validation issues with field paths and repair hints.
- Disallowed shallow implementation: compare raw selector strings at runtime or silently drop invalid selectors.
- Failing-first proof: `bundle://proof/SB01/transcripts/failing-first-capability-contracts-semantic.txt`
- Passing proof: `bundle://proof/SB01/transcripts/passing-capability-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB01/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Capabilities.Templates/CapabilityTemplateModels.cs`, `bundle://proof/SB01/transcripts/source-assertions.txt`
- Red-team negative case: `SB01_INV_POLICY_001` uses invalid `workspace write file` selector text and duplicate `deny-one` rule IDs.
- Downstream dependency check: SB06/SB10 can compile template/UI DTOs into domain policies once and block invalid policy materialization.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `CapabilityValidationIssue` | `repo://src/CanDoItAll.AgentFramework.Capabilities.Templates/CapabilityTemplateModels.cs` and `bundle://proof/SB01/transcripts/source-assertions.txt` | `repo://tests/CanDoItAll.Tests.Unit/CapabilityContractsTests.cs` | `bundle://proof/SB01/transcripts/passing-capability-contracts.txt` | `bundle://proof/SB01/transcripts/failing-first-capability-contracts-semantic.txt` |
| `SuppressedCapabilityDiagnostic` | `repo://src/CanDoItAll.AgentFramework.Capabilities.Access/CapabilityAccessPolicyEvaluator.cs` and `bundle://proof/SB01/transcripts/source-assertions.txt` | `repo://tests/CanDoItAll.Tests.Unit/CapabilityContractsTests.cs` | `bundle://proof/SB01/transcripts/passing-capability-contracts.txt` | `bundle://proof/SB01/transcripts/failing-first-capability-contracts-semantic.txt` |
| `EffectiveCapabilitySet` | `repo://src/CanDoItAll.AgentFramework.Capabilities.Abstractions/Capabilities.cs` | `repo://tests/CanDoItAll.Tests.Unit/CapabilityContractsTests.cs` | `bundle://proof/SB01/transcripts/dotnet-build-solution.txt` | `bundle://proof/SB01/transcripts/failing-first-capability-contracts-semantic.txt` |

## Anti-Stub Audit

- `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
