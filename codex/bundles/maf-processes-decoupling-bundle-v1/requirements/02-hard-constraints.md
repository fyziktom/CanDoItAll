# Hard Constraints

## Must Preserve

- Tool names from `AgentToolInvocationPolicyMetadata`.
- Tool policy classification in `ToolContractCatalog`.
- Tool capability classification in `ToolCapabilityRegistry`.
- `AgentProcessAccessMetadata` semantics.
- `AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(...)` behavior.
- Governed process automation finalizer behavior.
- Existing process API and template tool semantics.
- Existing dispatcher automation behavior.

## Must Not Do

- Do not move DTOs to a new contract assembly unless the subbundle explicitly says so.
- Do not change process tool names to provider-prefixed names.
- Do not collapse read/write access checks.
- Do not use reflection as the main decoupling mechanism.
- Do not make `CanDoItAll.AgentFramework.Tooling` reference `CanDoItAll.Modules.Processes`.
- Do not make `CanDoItAll.AgentFramework.Core` reference `CanDoItAll.Modules.Processes`.
- Do not loosen tests by converting assertions to vague count checks.
- Do not delete old tests without replacing them with stronger proof.

## Compatibility Strategy

Use a two-step migration:

1. Add provider seam while keeping the old process tool path.
2. Move process tools to provider and then remove the old path/reference.

This reduces risk and gives Codex a stable rollback point.
