# MAF runtime stabilization

This document records the runtime invariants for governed Microsoft Agent Framework execution in CanDoItAll.

## Structured output

- Machine-critical runs use `ExecutionRunRequest.StructuredOutput` and persist the contract key, output type, schema name, and schema description on the execution run.
- Pending-approval checkpoints preserve the structured-output contract so manual and auto-approved continuations resume with the same machine contract.
- Governed process-step continuations fail if the stored contract cannot be resolved.
- Successful governed runs validate the selected raw machine output through `DefaultAgentOutputValidatorRegistry` before completion.
- Validation logs and traces include the contract key and raw output hash; full raw payloads are not logged.

## Finalizer modes

`AgentFinalizerPolicy` defines exact-once typed finalizer tools for critical contracts. Current typed finalizers are:

- `submit_process_step_outcome`
- `submit_code_review_result`
- `submit_architecture_review_result`
- `submit_implementation_plan`
- `submit_test_plan`
- `submit_tool_execution_decision`
- `submit_process_state_patch`
- `submit_human_escalation_request`

Modes are carried through `ExecutionInvocationPolicy`, normalized into execution metadata by `ExecutionInvocationMetadata`, and resolved into `AgentRuntimeExecutionOptions` before the runtime build.

- `Required`: exactly one valid finalizer invocation must be present. Assistant text is display-only, and the finalizer payload replaces `ResponseText` before assistant transcript persistence.
- `Shadow`: a finalizer may be captured and compared to structured output, but structured output remains authoritative.
- `Disabled`: no finalizer tool is attached, no finalizer prompt text is appended, and no finalizer validation is performed.

Governed process automation sets required finalizer mode by default. Required and shadow finalizer instructions are compatible with JSON-schema response format: the model is instructed to return exactly one schema-conformant JSON object with no markdown, prose, code fences, or extra text. Required mode also instructs the model to call the typed finalizer after all other significant tool work. Shadow mode permits at most one finalizer call for comparison while keeping the final assistant response JSON authoritative. Deterministic process runtimes used by tests emit matching finalizer invocations only when the effective mode is not disabled.

`AgentRuntimeResponse.ToolInvocationTraces` records ordered tool calls with tool name, classification, sequence, timestamps, success, and failure text. Required governed finalizers must be the last significant tool invocation; mutation, validation, hosted-provider-native, local MCP, and hosted MCP calls after the required finalizer fail the run. Non-governed required runs record the same warning rather than treating assistant text as a hidden fallback.

## Repair and validation

Structured output validation is never skipped for governed machine-critical runs. If structured response text is invalid and the finalizer policy has already passed, the execution service can run a bounded repair loop.

- Governed process runs default to one repair attempt.
- Metadata clamps configured repair attempts to a maximum of two.
- Repair output is deserialized and validated with the same contract validator before it can replace response text.
- Required finalizer missing, duplicate, or invalid failures are not repaired as ordinary assistant text.
- Validator exceptions are converted to structured validation errors with code `agent.output.validator_exception`.

The default repair service is intentionally conservative: it can recover a single balanced JSON object from wrapped prose, then lets normal validation decide whether the candidate is acceptable.

## Tool policy

`DefaultAgentToolInvocationPolicy` evaluates function calls before the tool body runs.

Policy inputs include agent identity, tool name, redacted arguments, known-tool membership, classification, auto-approval state, provider approval capability, approval-wrapper effectiveness, and execution/process/step ids.

Policy decisions are explicit: `Allow`, `RequireApproval`, `Deny`, `SanitizeResult`, or `SkipExecution`. Middleware blocks `Deny` and `SkipExecution` through `AgentToolPolicyBlockGuard`, which throws `AgentToolPolicyBlockedException` with the tool name, decision kind, and reason. It also blocks `RequireApproval` when there is no effective approval path, so mutation/destructive tools cannot execute just because an approval decision was logged. Downstream tool `InvalidOperationException` and `NotSupportedException` failures are no longer reclassified as policy blocks.

During tool composition, approval-wrapped mutation tools are filtered when the provider cannot surface effective approval requests. Governed process automation fails the runtime build with a clear diagnostic in that state; exploratory/manual runs omit the unusable mutation tools and continue with the remaining safe tools. Auto-approved process automation suppresses approval wrapping intentionally, so mutation tools remain available for that governed path.

Sensitive argument names containing `api_key`, `apikey`, `authorization`, `credential`, `header`, `password`, `secret`, or `token` are redacted before signatures or logs are created.

## Provider gates

Provider features are resolved centrally through `ProviderProfileService.ResolveFeatureMatrix`.

Important flags:

- `SupportsFunctionTools`
- `SupportsStructuredOutput`
- `SupportsRunAsyncTypedOutput`
- `SupportsResponseFormatJsonSchema`
- `SupportsToolApprovalRequests`
- `SupportsApprovalRequiredAIFunction`
- `SupportsHostedTools`
- `SupportsHostedMcp`
- `SupportsLocalMcp`
- `SupportsServiceManagedHistory`
- `SupportsVision`
- `SupportsCompaction`

Structured output support is no longer tied only to Responses transport. Compatible OpenAI and Azure OpenAI chat-completion clients may use JSON-schema response format. Tool approval support remains narrower than function-tool support and must be checked independently.

Workspace-backed provider persistence stores the selected `providerTransport` explicitly in provider metadata/settings. Provider mapping reads that transport first and falls back to legacy display-name inference only for older records that do not yet carry explicit transport metadata.

Workspace provider UI defaults are resolved through `WorkspaceProviderCapabilityDefaults`. Ollama local and remote profiles default to `SupportsStructuredOutput = false`, and the workspace save path does not persist an editor-posted structured-output override for Ollama. Managed SQLite OpenAI chat-completions bootstrap profiles advertise structured output because the core feature matrix supports JSON-schema response format for OpenAI chat completions.

## Typed output API evaluation

The current MAF runtime path does not use `RunAsync<TOutput>` typed-output overloads. A repository search for `RunAsync<` under `src`, `tests`, and `docs` is empty. CanDoItAll still needs execution-time contracts because process automation selects contracts dynamically, persists those contracts through approval checkpoints, and routes finalizer policy through `AgentRuntimeExecutionOptions`. The active implementation therefore keeps `ChatResponseFormatJson` plus post-run validators/finalizers as the source of truth. Revisit typed `RunAsync<TOutput>` only when a concrete process path can carry a compile-time DTO end to end without losing dynamic contract persistence, repair, finalizer, and approval-continuation behavior.

## Workflow checkpoint bridge

Current workflow integration is deliberately narrow. CanDoItAll still uses its custom process dispatcher for process graph orchestration, while `WorkflowBackedAgentExecutionCheckpointBridge` uses the MAF `FileSystemJsonCheckpointStore` to preserve pending-approval checkpoint payloads and verify resume consistency.

This is checkpoint bridging, not full MAF Workflow orchestration of the process engine. The next adapter step is to evaluate selected process subflows for MAF workflow wrapping where typed routing, checkpointing, or human-in-the-loop behavior becomes simpler. A wholesale process-engine rewrite is not a near-term goal.

## Recovery guidance

Generic process automation delegates domain-specific retry guidance to recovery guidance providers. Runtime recovery paths must stay workload-neutral; product-specific advice belongs in opt-in agent instructions, reusable skills, or explicitly selected tools, not in the dispatcher.

## New-agent checklist

- Choose a provider profile whose feature matrix supports the required capabilities.
- Use structured output for machine-critical process, approval, branch, review, tool-decision, or state-patch decisions.
- Attach only tools the agent is allowed to use; set built-in tool `enabled` to `false` when disabled.
- Require an effective approval path for write/destructive tools unless the run is explicitly auto-approved.
- Use finalizer policy for exact-once critical decisions.
- Keep markdown summaries display-only.
- Validate with focused unit tests plus at least one integration path using fake/mock runtime behavior.
- Run live-provider validation behind environment guards when credentials and host dependencies are available.

## Validation commands

Primary proof for the post-audit hardening work is recorded in `docs/agent-runtime-hardening-verification.md`.
