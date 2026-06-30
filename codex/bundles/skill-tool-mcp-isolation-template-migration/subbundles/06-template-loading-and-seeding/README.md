# 06 Template Loading And Seeding

## Status

- `Completed`

## Objective

- Move default Skill, Tool, MCP capability definitions, and capability access policies into `Templates/Capabilities` and materialize the persistence seed catalog from the template pack.

## Success Criteria

- Default capabilities are defined in template files, not hardcoded seed helper calls.
- Default access policies for agents, processes, workflows, and compatibility operation rules can be loaded from templates and converted to typed domain policies.
- Seed materialization produces parity for existing stable IDs, keys, kinds, endpoints, tags, descriptions, and configuration JSON.
- Invalid templates fail before seeding with actionable validation messages.
- Template and seed failures use the structured diagnostics model with path, key, field, category, and repair hint.

## Covered Inputs

- R02, R03, R05, R09, R10, R11, R12, R13, R14, R15.

## Prerequisites

- SB01 contracts pass.
- SB02 tool descriptors pass.
- SB03 skill descriptors pass.
- SB04 MCP descriptors pass.
- SB05 hardening checkpoint passes.

## Exact Source References

- `repo://Templates/README.md`
- `repo://Templates/Agents/manifest.json`
- `repo://Templates/Agents/teams/dotnet-delivery/members/dotnet-application-developer/skills.json`
- `repo://src/CanDoItAll.AgentFramework.Persistence/CanDoItAll.AgentFramework.Persistence.csproj`
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedAssets.cs`
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs`
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs`
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs`
- `bundle://architecture/03-error-and-diagnostics-model.md`
- `bundle://architecture/04-implementation-quality-guardrails.md`
- `bundle://architecture/05-capability-access-policy.md`
- `bundle://inventories/04-capability-access-policy-test-inventory.md`
- `bundle://requirements/03-original-request-coverage-audit.md`

## Deliverables

- `Templates/Capabilities` manifest and descriptor files.
- `Templates/Capabilities/policies/capability-access-policy.json` or equivalent policy files, plus process/workflow/agent template field support.
- Template loader integration in seed materialization.
- Access policy loader and DTO-to-domain compiler with typed validation errors.
- Seed parity tests against existing default catalog.
- Migration strategy for embedded seed skill resources.
- Managed seed versioning rules for capability template changes.
- Negative template fixtures for duplicate keys, missing files, raw secrets, invalid allowedTools, and broken agent assignments.

## Dependency Impact

- SB07 hardens template/seed behavior before runtime reconnection.
- SB08 consumes template-backed capabilities during MAF runtime attachment.
- SB10 displays and edits template-backed capability types.
- SB11 uses seed parity as base regression proof.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Create `Templates/Capabilities` with manifests for skills, tools, MCPs, policies, and schemas.
2. Convert current hardcoded capabilities to template descriptors.
3. Add capability access policy fields to template models for agents, process definitions, process steps, workflow definitions, and workflow nodes.
4. Convert current process `AllowedOperations` and coarse tool/skill flags into typed compatibility policy inputs without changing behavior.
5. Replace seed builder hardcoded capability creation with template materialization.
6. Preserve stable GUID inputs and managed seed version behavior.
7. Add failing tests for duplicate keys, missing files, invalid names, raw secret fields, invalid policy selectors, ambiguous MCP tool selectors, and policies that try to grant unassigned capabilities.
8. Add parity tests comparing old known catalog keys to new materialized output.
9. Confirm agent `skills.json` assignments and process/workflow policy references resolve against template-backed catalog.
10. Add no-fallback tests proving invalid templates cannot silently use old hardcoded seed defaults.
11. Emit structured diagnostics for all template, access policy, and seed materialization failures.

## Scope Exceptions

- Do not delete old seed helper code until SB11 regression proof, unless it is fully unreachable and tests prove parity.
- Do not change agent template assignments in this subbundle except to correct references that fail validation.
- Do not change process/workflow behavior while introducing access policy fields; compatibility must be proven by tests.

## Do Not Do

- Do not add a fallback that silently rebuilds old hardcoded defaults when templates fail.
- Do not place capability template files under generated or ignored output directories.
- Do not merge template loading, seed materialization, and old seed compatibility into one overgrown file.
- Do not store policy selectors as opaque runtime strings after template loading.

## Acceptance Checklist

- All current default capability keys resolve from templates.
- `Templates/Agents/**/skills.json` assignments resolve without missing capabilities.
- Process/workflow policy references resolve to typed selectors or fail with exact path/key/field diagnostics.
- Template load failure blocks seed materialization with clear errors.
- Seed integration tests prove no duplicate canonical capability identities.
- Invalid template diagnostics include template path, key, field path, category, and repair hint.
- Materialization is deterministic and does not parse templates repeatedly during runtime composition.
- Existing process operation behavior is preserved through typed access policy compatibility tests.

## Proof Required

- Template loader unit tests.
- Seed parity integration tests.
- Agent assignment resolution tests.
- Access policy loader and compatibility operation tests.
- Negative no-fallback test transcripts.
- `proof/SB06/manifest.md`
- `proof/SB06/semantic-invariants.md`

## Browser Validation Logging

- N/A for template/seeding work. Browser-visible seeded catalog proof is SB11.

## Progression Gate

- `Passed` - template-backed seed materialization proves canonical catalog parity, agent assignment resolution, structured invalid-template diagnostics, and typed compatibility operation compilation.

## Execution Notes

- Added `Templates/Capabilities` with skill, tool, MCP, policy, schema, and other capability descriptor files.
- Added `CapabilityTemplatePackLoader`, `CapabilityTemplateSeedMaterializer`, policy/assignment validators, and process allowed-operation compatibility compiler in persistence.
- Replaced active hardcoded capability catalog construction in `SandboxWorkspaceSeedBuilder` with template materialization.
- Preserved old seed helper methods for the later SB11 cleanup gate; they are no longer the active seed catalog path.
- Added `CapabilityTemplateSeedMaterializationTests` covering template load, seed parity, invalid-template no-fallback behavior, agent assignment resolution, full seed integration, policy compilation, and allowed-operation compatibility.
- Validation transcripts and hashes are under `proof/SB06/`.

## Suggested Agent Prompt

```text
Implement subbundle SB06 only. Move default Skill, Tool, MCP, and capability access policy definitions into Templates/Capabilities and materialize seed data from the template pack after SB05 passes. Preserve stable IDs, keys, names, endpoints, process/workflow operation behavior, and configuration parity. No silent fallback to hardcoded defaults.
```

