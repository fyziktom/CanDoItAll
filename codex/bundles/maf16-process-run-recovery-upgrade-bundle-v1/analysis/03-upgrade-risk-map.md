# MAF Upgrade Risk Map

## Agent execution path

Risks:

- `AIAgent` API changes.
- `RunAsync` / streaming result shape changes.
- tool invocation loop behavior changes.
- tool approval handling changes.
- chat history/session persistence semantics change.

Mitigation:

- Create adapter-level tests around single-run, multi-turn, structured output, finalizer invocation, tool receipt capture, approval pending/resume, and provider fallback.

## Tool policy path

Risks:

- Function tool invocation argument shape changes.
- Middleware or tool call context API changes.
- Tool approval wrappers behave differently.
- MAF 1.6 hosted/local MCP support changes could bypass local policy if not wrapped consistently.

Mitigation:

- Keep `DefaultAgentToolInvocationPolicy` independent of MAF types.
- Add a single adapter from MAF tool invocation context to `ToolInvocationPolicyContext`.
- Red-team unknown/mutation tools.

## Handoff/A2A/workflows

Risks:

- A2A SDK v1.0 breaking change.
- Handoff role mutation fix may change message shape or remove a workaround.
- Workflow APIs may have changed around checkpointing, streaming, and evaluation metadata.

Mitigation:

- Isolate A2A/handoff in separate compile/test subbundle.
- Keep process-owned workflow/subprocess artifact mapping independent from MAF internals.

## Observability and OpenTelemetry

Risks:

- 1.6 release notes mention a breaking OpenTelemetry change around auto-wiring `OpenTelemetryChatClient`.
- CanDoItAll may double-wrap or lose trace data.

Mitigation:

- Add tests that tool receipts, finalizer invocations, context contribution traces, and execution logs remain persisted after upgrade.
