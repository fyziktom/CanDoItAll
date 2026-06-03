# SB02 Canonical tool capability registry and policy decomposition

## Status

Completed.
Critical foundation: **Yes**

## Objective

Replace split tool catalog/metadata/default-read behavior with one canonical tool capability registry and smaller policy components.

## Covered Inputs

R03, R04; source evidence E04, E05.

## Prerequisites

SB01 completed or at least resolver API shape stable. Read `ToolContractCatalog` and `AgentToolInvocationPolicy`.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ProcessToolOperationAuthorizer.cs`

## Deliverables

- `ToolCapabilityRegistry` as the canonical source for tool id, classification, operation requirements, target-scope requirements, side-effect descriptors, and approval defaults.
- No fallback-to-read for unknown known-tool names.
- Registry completeness tests comparing all known tool ids and API skill ids.
- Split policy services: operation requirement resolver, external target boundary policy, script side-effect policy, browser proof policy, repeat invocation guard, stale proof policy.
- Explicit metadata for `workspace_command_run`, `local_mcp_launch`, browser interaction tools, browser evidence tools, and provider-native/MCP paths.

## Dependency Impact

This subbundle affects downstream proof and must be treated as a dependency exactly as modeled in `bundle://plan/01-phase-plan.md`. If this subbundle fails, all downstream subbundles that depend on its runtime behavior or proof contract must be reopened.

## Validation Depth

Critical subbundle validation requires semantic adequacy proof: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, raw-note literal closure, changed-file hashes, and command/browser transcripts where applicable.

## Implementation Steps

1. Build a table of every known tool from `ToolContractCatalog`, API skill constants, project-structure/process tool names, browser tools, and workspace tools.
2. Assign each tool a classification and required process operations.
3. Make `Classify` fail unknown by default except for explicitly modeled provider-native/MCP tool families.
4. Move large policy branches into cohesive classes without weakening existing checks.
5. Add tests that fail if a tool exists in the catalog but not in the registry.
6. Add adversarial tests for `workspace_command_run`, browser click/type/screenshot, local launch, and unknown tool names.

## Scope Exceptions

No legacy compatibility exceptions were added. Browser proof-route validation was attempted, but Browser plugin URL policy blocked both generated `data:` and local `file:` routes; this is recorded in `proof/SB02/browser/browser-validation-blocked.md`.

## Do Not Do

Do not add a second registry. Do not keep `return Read` as the unknown fallback. Do not treat browser interaction as harmless read-only behavior.

## Acceptance Checklist

- [x] Source references were reopened before editing.
- [x] Implementation is the smallest correct change set for this subbundle.
- [x] Failing-first proof was captured for behavior-changing critical work.
- [x] Passing proof was captured after implementation.
- [x] Anti-stub audit was run.
- [x] Raw notes owned by this subbundle were closed or explicitly blocked.
- [x] Downstream dependency impact was reviewed before moving on.

## Proof Required

Registry completeness test, unknown-tool denial test, command-run operation test, browser interaction bounded-runtime test, provider-native/MCP explicit handling test, changed-file hashes.

## Browser Validation Logging

For browser-tool policy changes, include one proof route using browser actions and evidence tools, but keep full app E2E in SB04.

SB02 browser route validation was attempted and blocked by Browser plugin URL policy. Policy behavior is covered by `AgentToolInvocationPolicyTests`; full app/browser proof remains assigned to SB04 and SB08.

## Progression Gate

No SB04 real E2E may close until every tool used by the E2E has explicit registry metadata and operation requirements.

## Suggested Agent Prompt

You are implementing `SB02 Canonical tool capability registry and policy decomposition` in `fyziktom/CanDoItAll` on branch `development`. Read this subbundle README, the root README, `plan/01-phase-plan.md`, `traceability/`, and all exact source references before editing. Implement only this subbundle. Do not close it without the required semantic proof, transcripts, changed-file hashes, anti-stub audit, and raw-note closure update.
