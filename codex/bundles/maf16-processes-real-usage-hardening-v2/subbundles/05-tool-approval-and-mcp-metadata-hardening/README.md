# SB05: Tool Approval And MCP Metadata Hardening

## Status

- Completed

## Objective

Verify MAF 1.6 tool approval and middleware behavior across all tool types used by CanDoItAll.

## Covered Inputs

- RQ03: harden tool approval, middleware, and MCP metadata.

## Prerequisites

- SB02 adoption matrix must classify tool approval and metadata behavior.

## Exact Source References

- repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs
- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs
- repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs

## Deliverables

- Policy coverage for function tools, local MCP, hosted MCP, browser, shell/script, process, workspace, and project-structure tools.
- Red-team tests for unknown hosted tools, unknown `project_structure_*` tools, script side effects, and approval resume.

## Dependency Impact

- SB13 recovery/approval correctness and SB17 observability depend on accurate pending approval state.

## Validation Depth

- Critical semantic proof must show unknown or unsafe tool calls cannot bypass CanDoItAll policy.

## Implementation Steps

- Audit tool registration and policy routing.
- Add or update policy tests for all required tool classes.
- Preserve pending approval persistence and surface state.
- Update `proof/SB05`.

## Do Not Do

- Do not let MAF-hosted or MCP tools bypass CanDoItAll policy.
- Do not rely on stringly-typed ad hoc exceptions for tool classes.

## Acceptance Checklist

- Every required tool class is covered by policy tests or explicit exception.
- Approval resume proof is captured.
- MCP metadata forwarding is adopted or explicitly deferred.

## Proof Required

- Adversarial negative transcript for unsafe/unknown tool calls.
- Passing policy test transcript.
- Source assertions, anti-stub audit, and hashes.

## Browser Validation Logging

- N/A - no browser-visible behavior unless approval UI changes are made.

## Progression Gate

- SB09 and SB13 may depend on SB05 only after tool policy bypass tests pass.

## Suggested Agent Prompt

Audit all agent tool classes through CanDoItAll policy and harden approval/MCP metadata handling with adversarial tests.

## Closure Proof

- bundle://proof/SB05/manifest.md
- bundle://proof/SB05/semantic-invariants.md

