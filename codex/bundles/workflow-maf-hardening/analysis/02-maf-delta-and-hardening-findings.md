# MAF Delta And Hardening Findings

## Newer MAF workflow principles to preserve

Microsoft's current workflow documentation emphasizes these concepts:

- Workflows are explicitly defined business/process flows, unlike agents whose steps are dynamically chosen by an LLM.
- Workflows use typed messages, graph-based executors and edges, external/human integration, checkpointing, and multi-agent orchestration.
- `WorkflowBuilder` is the graph construction API for fixed topologies with type-validated message routing and superstep-based parallel execution.
- C# executor handlers should preferably be implemented with `[MessageHandler]` inside `partial` classes deriving from `Executor`, because this enables source generation, compile-time validation, better performance, and Native AOT compatibility.
- MAF validates type compatibility, graph connectivity, executor binding, and edge correctness when building workflows.
- MAF supersteps create important concurrency and checkpoint boundaries: executors triggered in a superstep run concurrently, then synchronize before the next superstep.
- Tool approval can be implemented with `ApprovalRequiredAIFunction` and by handling `FunctionApprovalRequestContent` in a loop.
- Agent Skills can package reusable instructions/scripts/resources, but file-based script execution via `SubprocessScriptRunner` needs production sandboxing, resource limits, input validation, allow-listing, logging, and audit trails.

## Likely implementation gaps to verify

Codex must verify these in repo-local code rather than assume:

1. **Package drift gap**: The project references MAF `1.6.2`, while `Microsoft.Agents.AI.Workflows` `1.7.0` exists. This does not automatically mean upgrade, but it does require an explicit baseline decision.
2. **Compiler boundary gap**: Repository `WorkflowGraph` definitions must compile/adapt into native MAF workflows through a narrow, testable service. If execution uses only custom graph traversal, newer MAF validation/checkpoint/event semantics are not being used.
3. **Typed message gap**: Templates are currently JSON-oriented. JSON may remain the persisted/interchange format, but native MAF execution should still use a deliberate typed envelope such as `WorkflowJsonMessage`, not ad-hoc `JsonElement`, `string`, or `object` everywhere.
4. **Graph validation gap**: The loader validates some fields, but a production workflow compiler should validate duplicate node IDs, duplicate edge IDs, start-node existence, edge endpoints, port compatibility, terminal reachability, route JSON validity, fan-in/fan-out constraints, and executor availability.
5. **Routing semantics gap**: Repository `BuiltInJsonV1` routing should have a deterministic implementation with tests for every operator, value kind, case sensitivity, missing values, invalid JSONPath, and fan-out target index behavior.
6. **Plugin executor gap**: Plugins need first-class workflow executor descriptors, schemas, capability flags, permission policies, timeout/retry/cancellation behavior, output artifact contracts, secret redaction, and deterministic fake tests.
7. **Approval gap**: Repository settings already include `RequireApprovalForToolUse`. Codex must verify this maps to MAF tool approval/HITL patterns for dangerous plugin/tool execution rather than remaining a passive flag.
8. **State/checkpoint gap**: Runtime policy prefers DurableTask and requires durable production runs. Codex must verify that preview and production paths are not accidentally equivalent and that checkpoint/resume semantics are not simulated only in UI.
9. **Event/artifact gap**: MAF emits workflow events; CanDoItAll stores artifacts. There must be a canonical event mapper with stable event IDs, run IDs, executor/node mapping, redaction, and artifact linkage.
10. **UI/seed migration gap**: UI and managed seed definitions must adapt to contract changes without breaking old user-managed definitions.

## Refactoring principle

Do not “fix” workflows by adding ad-hoc code inside pages, seed services, or plugin projects. Stabilize contracts first, then adapters, then runtime, then UI. Each subbundle must leave the code in a better architectural state even if later phases are not yet implemented.
