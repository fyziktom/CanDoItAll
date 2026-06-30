# 01 Contracts And Template Schema

## Status

- `Completed`

## Objective

- Define strongly typed capability contracts, exposure descriptors, access policy contracts, naming rules, structured diagnostics, validation result types, setup-test result types, and template schema models for Skill, Tool, and MCP capabilities before any runtime reconnection.

## Success Criteria

- New abstraction/schema projects compile independently from MAF.
- Template models validate capability keys, runtime names, schemas, side-effect metadata, approval defaults, secret bindings, setup-test declarations, and failure categories.
- Access policy models validate typed selectors, effects, scopes, precedence rules, and text conversion between templates/UI and runtime value objects.
- Error/result contracts carry capability key, kind, template path, field path, implementation key, correlation ID, masked detail, and repair hint where applicable.
- Existing capability keys and runtime tool names are represented as typed constants or generated registry entries, not free string switches.

## Covered Inputs

- R01, R03, R04, R05, R08, R10, R11, R12, R13, R14, R15.
- User requirement to create own projects with abstraction before implementation.
- Naming compatibility requirement.

## Prerequisites

- none

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolCapabilityRegistry.cs`
- `repo://src/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs`
- `repo://Templates/README.md`
- `bundle://requirements/02-naming-and-compatibility-standards.md`
- `bundle://architecture/03-error-and-diagnostics-model.md`
- `bundle://architecture/04-implementation-quality-guardrails.md`
- `bundle://architecture/05-capability-access-policy.md`
- `bundle://inventories/04-capability-access-policy-test-inventory.md`
- `bundle://inputs/01-source-artifacts.md`
## Deliverables

- Proposed projects added to solution as appropriate:
  - `CanDoItAll.AgentFramework.Capabilities.Abstractions`
  - `CanDoItAll.AgentFramework.Capabilities.Templates`
  - domain-specific `Skills`, `Tools`, and `Mcp` abstraction projects if not folded into the shared abstraction project.
- Typed descriptor models for Skill, Tool, MCP, MCP tool, setup tests, validation results, policy metadata, access policy rules, exposure descriptors, and stable IDs.
- `CapabilityAccessPolicy`, `CapabilityAccessRule`, `CapabilitySelector`, `CapabilityAccessEvaluationContext`, `EffectiveCapabilitySet`, suppression diagnostic contracts, and DTO-to-domain conversion helpers.
- Structured error categories and result records shared by template validation, setup tests, external calls, MCP lifecycle, and MAF adapters.
- JSON schema or equivalent validation model for `Templates/Capabilities`.
- Naming validator with explicit compatibility exceptions for existing names.

## Dependency Impact

- SB02-SB12 depend on these contracts. Weak contracts here will force duplicate DTOs in implementation projects or push stringly logic back into MAF.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Inventory all current config fields from MAF nested DTOs and seed helper methods.
2. Define capability identity types, exposure descriptor records, template descriptor records, and validation result types.
3. Define access policy domain contracts, selectors, effects, scopes, precedence rules, and suppression diagnostics.
4. Define parser/formatter/converter helpers for template/UI text to typed value objects and enums.
5. Define naming validation for capability keys, runtime tool names, MCP server keys, MCP tool names, operation keys, tags, and skill folder names.
6. Define external tool transport descriptors without allowing raw secrets.
7. Define MCP transport/lifecycle descriptors and list-tools setup result types.
8. Define structured diagnostic categories and required metadata for template, access policy, external tool, MCP, adapter, timeout, cancellation, and cleanup failures.
9. Add schema validation tests with valid and invalid fixtures.
10. Add access policy tests for conversion, selector matching, deny-over-allow precedence, require-plus-deny diagnostics, and allow-does-not-grant behavior.
11. Add diagnostic contract tests proving invalid fixtures report path/key/field/category/repair hint.
12. Add compatibility tests proving existing keys, names, and operation texts pass.

## Scope Exceptions

- Do not implement concrete internal tools, skill loading, or MCP clients in this subbundle.
- Do not reconnect MAF in this subbundle.

## Do Not Do

- Do not keep new descriptors private inside MAF.
- Do not infer runtime tool names from capability keys at execution time.
- Do not compare raw policy selector strings in runtime logic.
- Do not allow access policy `allow` rules to grant capabilities that are not already assigned/enabled.
- Do not introduce silent fallback defaults when templates are invalid.
- Do not model errors as unstructured strings or generic exceptions only.

## Acceptance Checklist

- Template validation reports template path, key, and field name on failures.
- Validation/setup results expose typed categories and repair hints for external tool and MCP failures.
- Existing default keys from seed builder and agent `skills.json` pass compatibility tests.
- Invalid names with spaces or unsupported characters fail.
- Invalid access policy selectors fail before materialization.
- Access policy precedence is deterministic and deny wins over allow.
- Required capability denied by policy produces a typed denied-required diagnostic.
- Raw secrets, raw headers, and raw environment variables fail schema validation.
- Contracts do not reference Blazor UI or MAF concrete runtime types unless isolated behind adapter-specific types.

## Proof Required

- `dotnet build CanDoItAll.slnx`
- Focused unit tests for schema, naming validation, access policy conversion, and precedence.
- `proof/SB01/manifest.md` with failing-first and passing transcripts.
- `proof/SB01/semantic-invariants.md` proving preserved keys/names and no silent fallback.

## Execution Proof

- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first-capability-contracts-semantic.txt`
- Passing targeted tests: `bundle://proof/SB01/transcripts/passing-capability-contracts.txt`
- Full solution build: `bundle://proof/SB01/transcripts/dotnet-build-solution.txt`
- Source assertions: `bundle://proof/SB01/transcripts/source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- Critical manifest: `bundle://proof/SB01/manifest.md`
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface.

## Progression Gate

- SB02-SB04 may start only after SB01 proof shows contracts compile, invalid templates fail predictably, structured diagnostics are typed, and existing compatibility names pass.
- Gate result: `Passed`. SB02-SB04 may start from these contracts.

## Suggested Agent Prompt

```text
Implement subbundle SB01 only. Define the shared strongly typed contracts, exposure descriptors, access policy contracts, and template schemas for Skill, Tool, MCP, and MCP tool capabilities. Preserve existing capability keys, runtime tool names, and operation texts as compatibility contracts through typed converters. Do not reconnect MAF or implement concrete tools yet. Capture schema, access policy, naming, and compatibility proof under proof/SB01.
```

