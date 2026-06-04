# Assumptions, Critical Path Risks, Validation Risks, and Reopen Triggers

## Assumptions

1. Codex can access the private repository and run the local development environment on Windows with Docker/PostgreSQL.
2. Codex can run real provider calls when an OpenAI provider profile and API key are configured. If no provider credential is available, real provider E2E must be marked `Blocked`, not silently replaced by a harness-only proof.
3. Existing tests may emit known dependency warnings; those do not block this follow-up unless they hide runtime/test failures.
4. Some legacy process definitions may have missing operation contracts. This follow-up must support a migration path, but governed live runs must fail closed until migration or explicit compatibility mode is chosen.

## Critical Path Risks

| Risk | Severity | Why it matters | Owning subbundle |
| --- | --- | --- | --- |
| Missing process operation contracts still permit tools | P0 | A process step could mutate product output or execute commands without an explicit contract. | SB01 |
| Tool names can fall through as read-only | P0 | A side-effecting tool can bypass approval/operation gates if not registered. | SB02 |
| SB08 proof bypasses automation | P0 | The process may appear proven while agent dispatch, tool usage, artifacts, and costing are untested. | SB04, SB05 |
| Provider usage does not reconcile with OpenAI billing | P1 | User sees larger provider billing than internal process cost. | SB03 |
| Dispatch heuristics scale poorly | P1 | New process families may break due to text-specific signals. | SB06 |
| Final closure gate trusts prose | P1 | Future bundles can again be marked complete with fixture-only evidence. | SB05, SB09 |

## Validation Risks

- Unit tests may pass while a real process still uses manual transitions.
- A proof script may create app files itself and then attribute them to a process run.
- Cost tests may use synthetic `AgentRunMetric` rows and miss raw provider usage details.
- Tool policy tests may cover only registered tools and not the entire known-tool catalog.
- UI proof may show empty states rather than the failure/unknown/cost states users need to interpret.

## Reopen Triggers

Reopen the owning subbundle immediately if any of these occur:

1. A governed live process step has no `AllowedOperations` and still executes a mutation, validation, launch, browser interaction, or external action.
2. `ToolContractCatalog.KnownToolNames` contains a tool not present in the canonical registry.
3. `workspace_command_run`, browser interaction, local launch, MCP, or provider-native tool calls can execute without explicit metadata and operation requirements.
4. A finalizer-short-circuit run creates nonzero provider usage but the process/run UI shows zero known cost without an unknown-usage marker.
5. A real E2E proof has `suppressAutomationDispatch = true` outside a fixture-only test.
6. A process E2E proof has empty `agent-execution-runs.json` while claiming agent-driven process validation.
7. A proof-quality validator accepts a critical proof with no failing-first transcript or no production producer path.
8. External OpenAI billing/export totals cannot be reconciled to internal response IDs, source phases, or usage observations.
