# 07 Template Seed Hardening Checkpoint

## Status

- `Ready after SB06`

## Objective

- Harden template loading, capability access policy loading, and seed materialization before MAF runtime reconnection, with emphasis on parity, deterministic diagnostics, managed seed stability, and no hidden fallback to old hardcoded defaults.

## Success Criteria

- Template-backed materialization proves parity for current default skills, tools, MCPs, policies, stable IDs, and agent assignments.
- Capability access policies prove current process/workflow operation behavior and reject invalid selectors before runtime.
- Invalid template packs fail with exact path/key/field/category/repair diagnostics.
- Managed seed version behavior is deterministic and does not create duplicate or churned capability identities.
- Static search and tests prove hardcoded seed defaults are not silently used when templates fail.

## Covered Inputs

- R02, R03, R09, R11, R12, R13, R14, R15.
- Mandatory requirement to preserve all functionality before reconnecting MAF.

## Prerequisites

- SB06 template loading and seed materialization proof passes.

## Exact Source References

- `bundle://templates/01-template-pack-design.md`
- `repo://Templates/Agents/manifest.json`
- `repo://Templates/Agents/teams/dotnet-delivery/members/dotnet-application-developer/skills.json`
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs`
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedNormalizer.cs`
- `bundle://architecture/03-error-and-diagnostics-model.md`
- `bundle://architecture/04-implementation-quality-guardrails.md`
- `bundle://architecture/05-capability-access-policy.md`
- `bundle://inventories/04-capability-access-policy-test-inventory.md`
- `bundle://analysis/03-codeanalytics-and-performance-review.md`

## Deliverables

- Seed/template hardening report.
- Parity manifest comparing current known defaults to template-backed materialization.
- Negative fixture suite for malformed templates and broken assignments.
- Negative fixture suite for malformed access policies, unknown keys, invalid enum text, ambiguous MCP tool selectors, and denied required capabilities.
- Managed seed version and migration dry-run evidence.
- Accepted-risk table for any deferred seed compatibility concern.

## Dependency Impact

- SB08 cannot reconnect MAF until template-backed seed data is stable and failure modes are deterministic.
- SB11 regression proof uses this parity as the baseline for process/workflow behavior.

## Validation Depth

- `Mandatory hardening checkpoint`

## Implementation Steps

1. Run parity tests comparing old catalog keys, runtime tool names, MCP server keys, stable ID sources, display names, descriptions, tags, operation classifications, and configuration JSON.
2. Run compatibility tests proving old process `AllowedOperations` and coarse capability flags compile to typed access policy behavior with no runtime change.
3. Run negative tests for duplicate keys, missing files, invalid names, raw secrets, missing `allowedTools`, unknown implementation keys, broken `skills.json` assignments, invalid access policy selectors, ambiguous MCP tool selectors, and policies that try to grant unassigned capabilities.
4. Confirm invalid templates and invalid policies do not fall back to old hardcoded seed construction.
5. Run a managed seed dry-run proving no duplicate identities or unintended seed version churn.
6. Review template loader/materializer/access policy compiler file sizes and split helpers if the implementation is becoming a large parser/materializer file.
7. Run focused performance checks for repeated file reads, repeated JSON options creation, repeated catalog parsing, repeated policy compilation, and LINQ-heavy hot loops.
8. Update `proof/SB07/manifest.md` and `proof/SB07/semantic-invariants.md`.

## Scope Exceptions

- Do not reconnect MAF in this checkpoint.
- Do not add UI/API setup flows in this checkpoint.

## Do Not Do

- Do not accept parity proof that only checks counts; compare identities and behavior-critical fields.
- Do not mask seed failures by continuing with partial default catalogs.
- Do not use production-only local files as proof fixtures.
- Do not accept access policy proof that only validates JSON shape; evaluator behavior and diagnostics must be tested.

## Acceptance Checklist

- Every existing default capability assignment resolves from `Templates/Capabilities`.
- Existing process/workflow operation restrictions resolve through typed access policies.
- Invalid template diagnostics include path, key, field, category, and repair hint.
- Invalid access policy diagnostics include path, field, bad value, selector/effect/scope context, and repair hint.
- Old hardcoded seed path cannot become an active fallback on template failure.
- Managed seed version behavior is deterministic.
- Focused performance findings are fixed or recorded.

## Proof Required

- Seed parity integration transcripts.
- Negative template fixture transcripts.
- Access policy compatibility and negative fixture transcripts.
- Managed seed dry-run output.
- Static scan summary for hardcoded fallback usage.
- `proof/SB07/manifest.md`
- `proof/SB07/semantic-invariants.md`

## Browser Validation Logging

- N/A. Browser-visible seeded catalog proof is SB11.

## Progression Gate

- SB08 may start only after SB07 proves template-backed seed behavior is stable, repairable, and no-fallback.

## Suggested Agent Prompt

```text
Implement subbundle SB07 only. Harden template-backed seed and capability access policy behavior before MAF reconnection. Prove parity, managed seed stability, current operation-rule compatibility, deterministic structured diagnostics, no hidden hardcoded fallback, and focused performance sanity. Do not reconnect MAF or UI.
```
