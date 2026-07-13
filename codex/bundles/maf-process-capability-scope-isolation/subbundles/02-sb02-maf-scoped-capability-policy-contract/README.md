# SB02 MAF Scoped Capability Policy Contract

## Status

- Status: `Completed`
- Criticality: `Critical enforcement foundation`
- Depends on: SB01

## Objective

Prepare MAF to accept typed runtime capability scope directives that can deny, require, and intentionally allow-only capabilities before tools, skills, MCPs, or runtime provider tools enter agent context.

## Covered Inputs

- Process steps must limit tools, skills, and MCPs.
- Forced tool/instruction carrier must be possible through required capabilities.
- Management-only steps must suppress development skills without editing the agent.
- REQ-MAF-003, REQ-MAF-004, REQ-MAF-005, REQ-MAF-010, REQ-MAF-011.
- NFR-002, NFR-003, NFR-005.

## Prerequisites

- SB01 complete.
- Read `bundle://inventories/02-capability-surface-inventory.md`.
- Confirm existing allowed-operation policy tests are understood before modifying access behavior.

## Exact Source References

- `repo://src/MAF/Capabilities/CanDoItAll.AgentFramework.Capabilities.Abstractions/CapabilityModels.cs`
- `repo://src/MAF/Capabilities/CanDoItAll.AgentFramework.Capabilities.Access/CapabilityAccessPolicyEvaluator.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Runtime/AgentRuntimeContextAssemblyModels.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.Policies.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeToolProviderComposer.cs`
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderDescriptor.cs`
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolMetadata.cs`

| Source | Required attention |
| --- | --- |
| `repo://src/MAF/Capabilities/CanDoItAll.AgentFramework.Capabilities.Abstractions/CapabilityModels.cs` | Existing descriptors, selectors, rules, diagnostics. |
| `repo://src/MAF/Capabilities/CanDoItAll.AgentFramework.Capabilities.Access/CapabilityAccessPolicyEvaluator.cs` | Deny/require semantics and non-restrictive allow behavior. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Runtime/AgentRuntimeContextAssemblyModels.cs` | Add or reference scoped runtime capability override. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.cs` | Pass non-empty required capabilities into access evaluation. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.Policies.cs` | Build scoped runtime policies. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeToolProviderComposer.cs` | Apply scoped policy to provider-generated tools and required provider tool capabilities. |
| `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderDescriptor.cs` | Provider key identity. |
| `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolMetadata.cs` | Provider tool metadata. |

## Scope

- Define typed MAF runtime scope contracts or DTOs.
- Add policy compilation for deny, require, and allow-only directives.
- Ensure allow-only does not fail open.
- Pass required capabilities to both initial catalog/configured-tool evaluation and runtime provider tool evaluation.
- Add provider key or implementation key metadata to provider-generated capability descriptors.
- Add diagnostics that include process/source context, rule id, selector kind, and reason.

## C# Architecture Impact

This is the central enforcement phase. It should reuse the existing capability access evaluator and descriptor model instead of creating process-specific enforcement inside MAF.

## Boundary Ownership

- AgentFramework capability abstractions own generic selector/rule concepts.
- MAF owns runtime composition and policy application.
- Processes do not appear in MAF contracts except through generic source ids already present in `AgentRuntimeContextIntent`.

## Dependency Direction

Potential acceptable dependency: `CanDoItAll.AgentFramework.Models -> CanDoItAll.AgentFramework.Capabilities.Abstractions` if scoped override DTOs are attached to `AgentRuntimeContextIntent`. Avoid dependencies from capability abstractions back to Models/Core/MAF.

## Dependency Impact

- Expected impact is inside AgentFramework capability abstractions/access, Models, Core metadata consumers, and MAF runtime composition.
- Downstream SB03 and SB04 must not expose process authoring fields until this enforcement layer is proven.

## Pattern Decision

Use a scoped policy compiler plus existing evaluator. If allow-only is needed, either:

- compile it into concrete deny rules for non-matching candidate descriptors, or
- extend the evaluator with explicit default-deny semantics and tests.

Do not rely on current `Allow` rules to restrict anything.

## Testability Contract

- Unit tests for deny by skill key, runtime tool name, MCP server, MCP tool, tag, operation classification, and provider key.
- Unit tests for required capability satisfied, missing, and denied.
- Unit tests proving allow-only suppresses non-matching capabilities.
- Unit tests proving invalid selectors throw or produce blocking validation diagnostics.
- Regression tests for existing process allowed-operation restrictions.

## Validation Depth

- Direct unit tests are mandatory for every directive effect and selector class.
- Regression tests are mandatory for existing allowed-operation policy behavior.
- Integration proof is deferred to SB06 after process handoff exists.

## Partial Class Policy

No new broad partials. If the compiler grows beyond a small helper, create top-level focused types such as `RuntimeCapabilityScopePolicyCompiler`.

## Implementation Steps

1. Define typed runtime scope records and validation.
2. Add policy compilation into `RuntimeCapabilityAccessPolicyBuilder`.
3. Pass non-empty required capabilities in `RuntimeCapabilityAccessPlanner`.
4. Extend runtime provider descriptor creation with stable provider identity.
5. Pass relevant required provider tool capabilities in `RuntimeToolProviderComposer`.
6. Add diagnostics and tests.
7. Capture proof in `proof/SB02/`.

## Do Not Do

- Do not expose allowlist semantics unless tests prove they are restrictive.
- Do not match runtime providers by display name or concrete type name.
- Do not swallow invalid scope metadata.
- Do not weaken existing operation contract restrictions.

## Acceptance Checklist

- Deny removes capabilities from effective descriptors and attachment paths.
- Required capabilities are no longer always empty.
- Required denial blocks governed execution.
- Provider-level suppression has a stable selector or is explicitly unavailable.
- Tests cover the evaluator/compiler behavior.

## Proof Required

- `proof/SB02/manifest.md`
- `proof/SB02/semantic-invariants.md`
- Production Behavior Artifact Matrix for new runtime scope contracts, metadata fields, diagnostics, and policy results.
- Test output for capability access tests.

## Browser Validation Logging

- N/A unless UI-visible diagnostics are added.

## Progression Gate

- SB03 must not start until MAF can enforce scoped deny/require behavior correctly.

## Suggested Agent Prompt

```text
Execute SB02 only. Add typed MAF runtime capability scope enforcement using existing descriptors/evaluator. Treat Allow as non-restrictive unless you deliberately implement allow-only semantics. Add direct tests and proof.
```
