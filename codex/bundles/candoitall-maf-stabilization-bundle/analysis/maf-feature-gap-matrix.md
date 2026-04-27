# MAF Feature Gap Matrix

| MAF capability | Current use | Gap | Target |
|---|---|---|---|
| Structured outputs through `ResponseFormat` / JSON schema | Used in `MafAgentRuntime.Session.cs` and process-step execution. | Lost on some continuation paths. Validators not broad enough. | Preserve contract across all run paths; validate before completion; repair/fail invalid output. |
| `RunAsync<T>` typed execution | Not found as a central pattern. | Current flow uses raw streamed response with response-format schema. | Evaluate whether selected non-streaming or test paths can use typed execution. Keep streaming if required, but validate after stream. |
| Agent run middleware | Used mainly for logging/telemetry. | No central structured-output/finalizer/tool-policy enforcement. | Add agent-run policy middleware and runtime-scope metadata. |
| Function invocation middleware | Used for progress/telemetry. | Not used as the central pre-execution tool firewall. | Add allow/deny/approval/sanitization/repeated-call policy before execution. |
| `IChatClient` middleware | Not clearly used as policy layer. | Request/response policy and provider capability checks could be centralized. | Use where it improves provider capability enforcement and diagnostics. |
| Function tools via `AIFunctionFactory.Create` | Used extensively. | Critical-decision finalizer tools not implemented. | Add exact-once typed finalizer tools for critical workflow decisions. |
| Tool approval | Used through `ApprovalRequiredAIFunction`. | Continuation needs to preserve structured-output contract; hosted/provider-native approval semantics must be capability-gated. | Approval flow stores contract, policy context, and pending decision metadata. |
| Workflows/orchestrations | Package referenced; checkpoint store used. | Process orchestration itself is custom. | Add adapter/harness for MAF workflow step boundaries and selected multi-agent subflows. |
| Checkpointing | Used for pending approval checkpoint payloads. | Not yet a general step-boundary recovery model. | Persist validated step state and MAF workflow checkpoint metadata for long-running steps. |
| Agent sessions | Used with serialization/deserialization. | Session/history may become hidden process state; prompt replay behavior needs explicit tests. | Process state remains source of truth; session is short-lived context only. |
| Context providers | Used for memory/AI context/RAG/compaction. | Need bounded process-state provider and stronger compaction gating. | Inject concise process state and evidence; keep history compact and controlled. |
| MCP tools | Strong local/hosted MCP handling exists. | Policy should be unified with all tool categories. | Feed MCP tools into the same central function invocation policy. |
| Observability | OpenTelemetry and logging exist. | Need validation/repair/finalizer/policy outcome spans and redaction rules. | Trace every run, tool decision, structured-output validation, retry, repair, and finalization. |
