# Scope Inventory

## In Scope

- Microsoft Agent Framework runtime adapter in `src\CanDoItAll.AgentFramework.Maf`.
- Core execution contracts in `src\CanDoItAll.AgentFramework.Core`.
- Shared model contracts in `src\CanDoItAll.AgentFramework.Models`.
- Process automation dispatch in `src\CanDoItAll.Modules.Processes\Automation\Dispatch`.
- Focused unit/integration tests under `tests`.
- Documentation under `docs`.

## Out Of Scope

- Broad UI redesign.
- Provider credential management refactors.
- Replacing all existing workspace/process tools.
- Full process-definition patch engine unless an existing agent path directly mutates process state from model output.
- Live provider conformance tests requiring real API keys.

## Existing Safe Patterns

- Tools are registered with `AIFunctionFactory.Create(...)`.
- Tool and artifact validation already exists around required workspace operations.
- Execution state, metrics, and raw response text are already persisted for observability.

## Existing Unsafe Patterns

- Workflow outcome is requested as an HTML comment in assistant text.
- Regex and `JsonDocument` parsing decide process status and branch route.
- Structured response format is not configured for machine-critical process outputs.
- No central typed validation/repair runner exists for structured outputs.
