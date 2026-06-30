# Capability Access Policy Request

## Raw Requirement Summary

Agents running inside processes, workflows, or other contexts must be able to limit or forbid use of selected tools, skills, MCP servers, and MCP tools.

## Required Properties

- Restrictions must be generic enough for agent, process, workflow, template, and UI use.
- Restrictions must have a healthy bounded shape, not an arbitrary policy scripting language.
- Runtime code must avoid magic strings and use proper DTOs, enums, typed value objects, factories, builders, and conversion helpers.
- Template/UI text representations are allowed only at boundaries and must be converted to typed runtime models with validation.
- Adding a new tool, skill, MCP server, or MCP tool must not require changing the suppression mechanism if the new capability exposes the common metadata.
- The architecture must use C# strengths: strong typing, DI, explicit interfaces where useful, and testable services.
- The solution must be analyzed against .NET maintainability/performance anti-patterns and Microsoft guidance.
- Tests must cover unit, integration, UI/API, and e2e behavior, including denied required capabilities and clear diagnostics.

## Bundle Response

- `architecture/05-capability-access-policy.md` defines the target access policy model.
- `inventories/04-capability-access-policy-test-inventory.md` defines the required proof.
- R15 in `requirements/01-normalized-requirements.md` tracks the requirement across all subbundles.
- SB01-SB12 now include access policy design, hardening, template loading, MAF reconnection, UI/API editing, regression proof, and cleanup guardrails.
