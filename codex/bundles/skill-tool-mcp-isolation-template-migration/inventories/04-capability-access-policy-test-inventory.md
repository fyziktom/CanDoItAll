# Capability Access Policy Test Inventory

## Purpose

This inventory turns the agent/process/workflow capability restriction requirement into concrete proof obligations. It applies to skills, tools, MCP servers, and MCP tools.

## Current Seams To Reconnect

| Existing seam | Current limitation | Planned proof |
| --- | --- | --- |
| `AgentCapabilityRequirementEvaluator` | Handles required capabilities but not deny/suppress policy across all capability kinds. | Denied required capability test emits a typed diagnostic with denying rule scope and repair hint. |
| `ProcessToolOperationAuthorizer` | Tool-only and string operation based. | Existing `AllowedOperations` inputs parse into typed operation classifications and produce equivalent tool access decisions. |
| `MafAgentRuntime.Capabilities` skill/tool/MCP attach paths | Hardcoded attach-time filters are spread across partial methods. | Runtime composition consumes `EffectiveCapabilitySet`; static scan proves direct hardcoded suppression is removed or compatibility-wrapped. |
| Agent settings `permissions` and `access.workspaceTools` | Coarse flags without common policy shape. | Agent policy loader converts flags into typed access rules and preserves compatibility. |
| Process and workflow templates | No generic typed policy for forbidding skill/tool/MCP use by step or node. | Process step and workflow node fixtures deny representative skill, tool, MCP server, and MCP tool. |
| Capability setup UI | Skill/MCP setup exists, tool setup is missing, and access restriction editing is not first-class. | UI component/API tests round-trip access policies through typed DTOs and catalog-backed selectors. |

## Unit Coverage

| Test group | Required cases |
| --- | --- |
| Text conversion | Valid and invalid `CapabilityKind`, `CapabilityAccessEffect`, `CapabilitySelectorKind`, `CapabilityAccessScope`, `CapabilityKey`, `RuntimeToolName`, `McpServerKey`, `McpToolName`, `ProcessOperationKey`, and tag values. |
| Policy compilation | DTO-to-domain conversion, duplicate rule IDs, unsupported selector/effect combinations, missing selector value, invalid scope inheritance. |
| Precedence | Retired/unavailable excluded first, system deny wins, deny beats allow, allow does not grant missing assignment, required plus denied fails with denied-required diagnostic. |
| Selector matching | Match by kind, key, tag, operation classification, runtime tool name, MCP server, MCP tool within server, implementation key. |
| Generic participation | Fake new capability descriptor is suppressed by tag/kind without changing evaluator code. |
| Diagnostics | Invalid selector, unknown key, ambiguous MCP tool, denied required capability, and suppressed runtime attachment include category, path/key/field when known, rule scope, reason, and repair hint. |

## Integration Coverage

| Scenario | Expected proof |
| --- | --- |
| Existing process `AllowedOperations` compatibility | Old operation lists produce equivalent effective tool access while using typed operation keys internally. |
| Process step denies mutation tools | Runtime attaches validation/read tools but excludes mutation tools with manifest diagnostics. |
| Process step denies all skills | Existing skill exclusion behavior is preserved through policy rules rather than hardcoded MAF checks. |
| Workflow node denies external MCP servers | External MCP descriptors are excluded while internal hosted MCP descriptors remain candidates. |
| MCP server allowed, child tool denied | Server can start/list tools, but denied MCP tool is not exposed to the agent. |
| Required capability denied | Execution fails before runtime call with a specific denied-required diagnostic. |
| Template materialization failure | Invalid `capabilityAccessPolicy` blocks seed/materialization and does not fall back to old defaults. |

## UI/API Coverage

| Surface | Required cases |
| --- | --- |
| Capability access editor | Catalog-backed key picker, tag selector, operation classification selector, MCP server/tool picker after list-tools, rule reason field, preview of suppressed capabilities. |
| API DTO validation | Invalid enum text, unknown capability key, ambiguous MCP tool, and unsupported selector scope return structured validation results. |
| Process/workflow template editor | Add, remove, and reorder rules without raw string entry for normal cases. |
| Diagnostics display | Denied required capability shows rule scope, selector, reason, correlation ID if present, and repair hint without leaking secrets. |

## E2E Coverage

| Flow | Required outcome |
| --- | --- |
| Read-only process step | Step denies mutation operation classifications and still completes validation/read work. |
| Workflow external MCP denial | Workflow node forbids external MCPs; run completes without exposing those tools. |
| Denied required capability | Run blocks with actionable diagnostic, not a generic start or attach error. |
| MCP setup plus process denial | User lists tools during setup, selects allowed tool, then process step denies that MCP tool and runtime manifest records exclusion. |

## Anti-Patterns To Block

- Policy evaluator compares raw strings instead of value objects.
- Template invalid values are silently ignored.
- `allow` rules grant capabilities not already assigned to the agent.
- MAF applies a second private suppression pass after receiving the effective set.
- Tests only assert counts and not exact identities or diagnostics.
- Negative tests use broad exception assertions instead of typed failure categories.
- UI tests rely on raw JSON entry for the normal path.
- External tool/MCP denial is reported as generic startup failure.
