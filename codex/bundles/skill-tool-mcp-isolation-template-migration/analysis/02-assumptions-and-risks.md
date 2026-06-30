# Assumptions And Risks

## Assumptions

- `Templates/Capabilities` is acceptable as a new sibling under `Templates/`, matching the existing README guidance for future template sets.
- Runtime tool names remain snake_case for compatibility; capability/template keys remain kebab-case because existing agent `skills.json` files already refer to kebab-case capability keys.
- The existing `CanDoItAll.AgentFramework.Tooling` project can either be evolved or bridged, but a fuller capability abstraction layer is still needed for schemas, setup tests, external calls, and template loading.
- MAF will remain the provider bridge and execution adapter, but it should consume dedicated capability services instead of owning skill/tool/MCP realization.

## Critical Path Risks

- Breaking capability keys will detach agent template assignments from seeded catalog capabilities.
- Renaming runtime tool names will break process policies, finalizer expectations, stored tool receipts, and any prompt/tool allowlists.
- Moving config DTOs out of MAF too early can produce duplicate models or subtle serialization drift.
- Reconnecting MAF before dedicated projects are tested would preserve the current coupling under new names.
- External tool support can become an unbounded command execution feature unless the schema includes explicit transport, timeout, working directory, environment/header bindings, approval, and side-effect declarations.
- MCP lifecycle management can leak processes if internal hosted and local stdio server ownership is not explicit.

## Validation Risks

- Unit-only proof is insufficient because seed normalization, process policies, UI setup, and MAF runtime composition all interact.
- Browser-visible setup flows need component and Playwright proof, not just API tests.
- MCP setup testing must prove start/list-tools behavior using a deterministic fake MCP server and must not require secrets or a user-specific local machine setup.
- External generic tools need fake command/http invokers so tests do not rely on Python, PowerShell, or external executables being present beyond the controlled fixtures.
- Existing tests may pass while templates are ignored if the seed builder still supplies fallback hardcoded defaults. Template loader tests must include failing-first cases where code defaults are unavailable.

## Reopen Triggers

- Any subbundle proposes deleting current MAF or persistence code before replacement projects have passing unit and integration proof.
- Any compatibility shim silently falls back to old hardcoded capability definitions after a template load failure.
- Any generated template uses raw secrets, raw environment variables, or raw headers instead of secret bindings.
- Any setup test returns `PendingReview` for a machine-executable check that should be deterministic.
- Any UI editor allows a tool or MCP with no input schema, no approval/side-effect classification, or no test result.
- Any final regression omits process/workflow tests that exercise existing workspace, .NET, browser, finalizer, and MCP tool families.
