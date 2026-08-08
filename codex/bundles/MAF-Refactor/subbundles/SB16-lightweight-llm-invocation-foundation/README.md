# SB16-lightweight-llm-invocation-foundation: Provider-backed lightweight LLM invocation and future ordinary-chat foundation

## Metadata

- Phase: E — Continuation and lightweight inference
- Depends on: `SB11-runtime-split-checkpoint`, `SB15-versioned-runtime-state-and-continuation`
- Checkpoint: No
- Target executor: Claude Code
- Preferred model: Claude Fable 5
- Reasoning profile: maximum/deep available (`xHigh` intent; no literal Claude CLI flag is assumed)
- Baseline repository: `fyziktom/CanDoItAll`, branch `development`

## Goal

Introduce a lightweight, provider-neutral LLM invocation boundary that bypasses agent definitions, MAF agent sessions, capability composition, tools, memory, handoffs, approvals, finalizers, and product context; migrate ordinary workflow LLM nodes to it; and establish the application contracts needed for a future ordinary multi-turn LLM chat.

## Why this subbundle exists

The current workflow invoker creates a temporary `AgentDefinition` and `ChatSessionRecord`, enters the full agent runtime, and can infer project scope from payload. This makes a simple model transformation expensive, non-deterministic, and too close to product authority. The repository already contains provider-neutral `IProviderChatCompletionDriver` implementations and a provider runtime pool, which are a better execution foundation for lightweight inference.

## Scope

- Define SDK-free `ILlmInvocationPort` and optional `IStreamingLlmInvocationPort` contracts.
- Define repository-owned message, attachment, model-setting, response-format, usage, finish, and failure records.
- Implement a provider-backed adapter over existing provider runtime/driver infrastructure.
- Migrate `MafWorkflowLlmComponentInvoker` or replace it with a correctly owned workflow adapter that uses the lightweight port.
- Preserve provider/model selection, usage observations, cancellation, JSON response format/schema behavior, and sanitized failures.
- Define and test the application boundary for a future ordinary LLM conversation service without building its UI in this bundle.

## Non-goals

- Do not migrate explicit agent-capable workflow nodes that intentionally need tools, memory, context, handoffs, approvals, or finalizers.
- Do not add an ordinary-chat UI, persistence schema, or product-context integration yet.
- Do not expose provider SDK or MAF SDK types in the lightweight contracts.
- Do not infer authority or workspace scope from message/payload content.

## Required SharedInfo skills

- `csharp-provider-tool-plugin-isolation`
- `csharp-project-boundary-extraction`
- `csharp-factory-builder-composition`
- `csharp-testability-contracts`
- `canonical-model-review`

Read `../../sharedinfo/required-skills.md` and the corresponding installed skills before editing.

## Pre-flight

1. Verify dependencies are `Unlocked`.
2. Record current HEAD and inspect the current provider runtime/driver architecture.
3. Read `../../architecture/14-lightweight-llm-and-ordinary-chat-foundation.md` and ADR-008/ADR-010.
4. Use CodeAnalytics to map all workflow LLM callers and provider chat-completion drivers.
5. Characterize current workflow output, schema, usage, provider selection, failure, and cancellation behavior.
6. Copy the proof manifest and create `proof/SESSION-HANDOFF.md`.

## Detailed implementation tasks

1. Select a cohesive SDK-free contract owner, preferably a small `Llm.Abstractions` or equivalent runtime-neutral project. Do not place the contracts in MAF or a UI module.
2. Define `LlmInvocationRequest` with immutable system instructions, ordered repository-owned messages, explicit provider/profile and model selection, model parameters, response format/schema, bounded attachments, correlation/causation IDs, deadline/budget, and streaming preference. No agent, capability, session, authority, workspace, process, or UI types are allowed.
3. Define `LlmInvocationResult` with response messages/text, provider/model identity, finish disposition, usage observations, cached/reasoning tokens where available, response-format evidence, provider compatibility evidence, and sanitized failure classification.
4. Define streaming updates with stable sequence and terminal semantics if current providers can support them without buffering the whole response. A non-streaming adapter may compose over streaming, but there must be one accounting source of truth.
5. Implement the adapter above `IProviderRuntimePool` / `IProviderChatCompletionDriver` or an equivalent provider application service. Reuse dispatch lanes, credentials, model normalization, retry/blank-response policy, and usage accounting deliberately. Do not route through `MafAgentRuntime`.
6. Separate provider diagnostics/model administration from normal invocation; do not make the lightweight port another broad provider gateway.
7. Rewrite ordinary workflow LLM invocation to call the port. Remove temporary agent/session creation, approval suppression, capability lists, memory lists, context contributors, and `projectId` payload scope inference.
8. Preserve workflow-specific usage mapping and schema validation outside the provider adapter when it belongs to workflow semantics.
9. Define a future `ILlmConversationService` application contract that owns transcript persistence, conversation metadata, summarization/compaction policy, and provider/model selection while delegating each inference to the stateless port. Do not implement it by constructing an agent with disabled tools.
10. Add source guards proving ordinary workflow/lightweight paths do not reference agent execution contracts or MAF agent types.

## C# Architecture Impact

This establishes a reusable inference layer distinct from agent execution. It must be small enough for workflow transforms and future ordinary chat, while agent execution remains an explicit higher-level capability.

## Boundary Ownership

- LLM abstractions own stateless invocation contracts.
- Provider runtime owns dispatch, credentials, provider drivers, and provider protocol mapping.
- Workflow adapter owns workflow input/output and usage projection.
- A future ordinary-chat application service owns transcript and conversation behavior.
- Agent runtime owns tools, memory, context, handoffs, approvals, finalizers, and agent session state; none leak downward.

## Dependency Direction

Workflow/Application -> LLM Abstractions. Provider-backed implementation -> LLM Abstractions + Provider abstractions/runtime. Composition root -> implementation. No LLM Abstractions -> MAF/Core/Modules/UI/provider SDK.

Any `.csproj` change requires before/after project-reference evidence and a cycle check. Do not create a broad `Common` project.

## Pattern Decision

Use a stateless port and provider adapter. Add a separate application conversation service later for ordinary chat. Reject a reduced `IAgentRuntime`, a tool-disabled agent, or a workflow-specific provider client as the common boundary.

## Testability Contract

- Direct text invocation with fake provider driver.
- Ordered multi-message invocation.
- Streaming sequence/terminal behavior or explicit unsupported result.
- Model parameters and response-format/schema mapping.
- Usage and cached/reasoning token accounting.
- Cancellation/deadline propagation.
- Sanitized provider failure and blank-response retry behavior.
- Bounded image attachments where supported.
- Payload containing project IDs/paths cannot acquire authority, workspace services, context contributors, or tools.
- Workflow parity tests for text, JSON, usage, failure, and cancellation.
- Source assertions for no `AgentDefinition`, `ChatSessionRecord`, `IAgentExecutionRuntime`, capability composer, or MAF agent session in ordinary LLM paths.
- Future ordinary-chat contract test demonstrates transcript ownership above the stateless port without implementing UI.

## Partial Class Policy

- Do not add provider or workflow partial classes as the final boundary.
- Do not hide the agent runtime behind an `LlmHelper`.
- Do not duplicate provider protocol logic outside the provider driver architecture.

## Architecture Proof Required

- Provider call-chain before/after map.
- Exact reason for chosen contract/implementation projects.
- Direct fake-driver tests and negative no-agent tests.
- Workflow parity and usage evidence.
- Dependency/cycle report.
- Source guards and old-path deletion proof.
- Performance evidence showing no capability/tool/context assembly for lightweight calls.

## Validation commands

- Focused provider runtime/driver tests.
- New lightweight LLM unit and composition tests.
- Workflow adapter/compiler/backend/usage tests.
- Architecture/source guards.
- Release build.

## Acceptance criteria

- Ordinary workflow LLM nodes use the lightweight port.
- The port never constructs an agent/session or assembles capabilities/context.
- Provider behavior, usage, schema, cancellation, and failures are preserved.
- Message/payload data cannot select authority.
- The contracts can support a future ordinary chat without adopting agent semantics.

## Stop and repair conditions

- Stop if the port gains tools, memory, approvals, finalizers, handoffs, workspace scope, product context, or opaque universal options.
- Stop if provider SDK types enter abstractions.
- Stop if the implementation duplicates provider credential, dispatch-lane, retry, or usage logic instead of reusing the existing provider boundary.
- Stop if ordinary chat is modeled as an agent with everything disabled.

## Required deliverables

- lightweight LLM contracts and provider-backed implementation
- migrated workflow LLM path
- future ordinary-chat application contract/design
- tests, source guards, dependency proof, and performance evidence

## Downstream unlock

SB17 may start when workflow parity, direct-port tests, no-agent source guards, and dependency checks pass.

## Claude Code execution profile

- Primary executor: Claude Code.
- Preferred model: Claude Fable 5.
- Reasoning profile: use the deepest/maximal reasoning mode available in the installed Claude Code version. The phrase `xHigh` expresses intent only; do not invent or require a non-existent CLI flag.
- Fallback: Claude Opus 5 when that model is configured and available in the operator environment; otherwise the best available high-capability Claude model configured in the environment. Preserve this subbundle's proof, stop conditions, and architecture gates when switching models.
- Work on exactly this subbundle. Do not opportunistically implement a later subbundle because related files are open.
- Use installed SharedInfo skills and CodeAnalytics MCP as evidence sources. Treat MCP summaries as orientation, then inspect exact source and project files before editing.
- Persist decisions, commands, failures, and remaining work in the subbundle proof directory so another Claude session or model can resume without conversational memory.

## High-risk adaptation points

- Reusing `MafAgentRuntime` under a smaller interface would retain agent construction, capability composition, session, approval, finalizer, and context overhead.
- The repository already has provider-neutral chat completion drivers and a provider runtime pool; the lightweight port should normally compose above that boundary.
- Preserve provider/model selection, model parameters, messages, attachments, streaming semantics, usage, cancellation, structured response format, retry policy, and sanitized failures.
- Future ordinary LLM chat needs a transcript/application service above the stateless invocation port, but it must not be implemented as an `AgentDefinition` with disabled tools.
- Workflow payload content, including `projectId` or paths, is data and cannot grant workspace authority.

## Safe cutover sequence

1. Define a stateless SDK-free invocation port and streaming companion over repository-owned messages/results.
2. Implement a provider-backed adapter using the existing provider runtime/driver boundary; do not construct an agent/session/capability graph.
3. Validate the port directly with fake drivers and provider-runtime lifecycle tests.
4. Migrate ordinary workflow LLM nodes and compare output, schema, usage, cancellation, and failures.
5. Leave future ordinary-chat UI out of scope, but add a tested application-layer conversation design that can call the same port.

## Post-change verification and bugfix procedure

1. Reproduce with fixed operation/run/session/context/authority/scope identifiers and a fake provider or deterministic fixture where possible.
2. Identify the failing stage from persisted activity and telemetry before editing: admission, context, authority, scope, composition, provider, session, tool, approval, output/finalizer, persistence, process, workflow, or UI refresh.
3. Add a failing regression test at the owner boundary. Do not patch the caller merely because the symptom appears there.
4. Compare against SB00 characterization/golden evidence and inspect changed project references and runtime/tool manifests.
5. Apply the smallest cohesive fix, then run focused tests, architecture guards, and the current checkpoint suite.
6. Update `proof/proof-manifest.json`, the risk register, and `proof/SESSION-HANDOFF.md` with the root cause and remaining uncertainty.

## Durable session handoff

Before ending a Claude Code session, update `proof/SESSION-HANDOFF.md` with:

- current commit and working-tree state;
- completed checklist items and changed files;
- exact commands and test results;
- CodeAnalytics snapshot/dependency evidence;
- selected cutover path/flag and observed telemetry;
- unresolved failures with correlation IDs and owning stage;
- the next smallest safe action;
- anything a fallback Claude model must not redo or reinterpret.

Do not rely on chat history as the only handoff mechanism.
