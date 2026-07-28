# Current-State Architecture

## Package Topology

The current MAF version is repeated in multiple project files:

- main MAF adapter: stable core/OpenAI/workflows plus A2A preview;
- workflow adapter: stable core/workflows;
- hosting: Hosting.A2A preview.

`Directory.Build.props` exists but currently contains only shared compiler/default-item settings. A minimal migration can add two shared properties there:

- one stable MAF version;
- one preview A2A MAF version.

This avoids repository-wide Central Package Management churn while eliminating release-train skew.

## Runtime Lifetime

`MafAgentRuntime` is a singleton service, but the mutable runtime graph is not:

1. resolve provider and execution options;
2. create a `RuntimeBuildResult`;
3. compose current tools, context providers, memory, attachments, policy state, and finalizer state;
4. create/restore a session;
5. run through the provider streaming runner;
6. collect updates and approvals;
7. serialize eligible session state;
8. dispose the runtime build and all owned resources.

Handoff builds create one runtime build per participant and aggregate their disposal.

This is the correct boundary. MAF 1.15 does not change it.

## Preparation and Agent Loading

The completed agent-loading refactor intentionally caches immutable definitions, provider snapshots, and preparation blueprints. Existing architecture evidence explicitly forbids caching:

- live `AIAgent` instances;
- provider clients with mutable conversation state;
- `AgentSession`;
- mutable tool instances;
- MCP connections owned by a run;
- pending approvals;
- request-specific authorization or context.

The upgrade must not regress this design.

## Agent Construction

`MafRuntimeAgentFactory` creates `ChatClientAgentOptions` with:

- ID/name/description;
- model-compatible `ChatOptions`;
- per-run tool list;
- current context providers;
- optional framework-managed `ChatHistoryProvider`;
- `RequirePerServiceCallChatHistoryPersistence`.

The inspected construction does not explicitly set:

- `UseProvidedChatClientAsIs`;
- `DisableApprovalResponseBinding`;
- `DisableApprovalNotRequiredFunctionBypassing`.

Therefore the default 1.15 middleware is expected to activate unless `IMafProviderAgentFactory` mutates the options or supplies a pre-decorated client path. SB01 must inspect every provider factory implementation and prove the effective pipeline.

## Tool Governance

The application wraps the agent with its own invocation middleware. It:

- classifies reads and mutations;
- recognizes known tools and approval-wrapped tools;
- applies workspace and external-target authorization;
- enforces read-only aliases;
- inspects governed script content and side-effect manifests;
- blocks or returns recoverable policy denials;
- captures tool ownership, audit scope, traces, and telemetry;
- short-circuits after a required finalizer invocation.

This policy is application governance, not a workaround for the missing 1.15 binding. MAF binding must complement it.

## Approval Continuation

Current continuation has two stores:

1. process-local cache of raw `ToolApprovalRequestContent`;
2. persistent `PendingToolApprovalRecord` with approval ID, call ID, tool name/kind, endpoint detail, and arguments JSON.

On restart, the request is reconstructed and `CreateResponse(approved)` is called. Current risks:

- one boolean applies to every pending approval;
- a missing approval ID falls back to a new random GUID;
- the persistent record is a lossy reconstruction unless all tool-call shapes are covered;
- a 1.13 session has no `_pendingApprovalRequests` MAF state for the 1.15 binding;
- session attachment scrubbing must be proven not to remove new state-bag entries;
- process-local cache and persistent state need one documented authority and lifecycle.

## Session Model

The runtime supports:

- framework-managed history;
- provider-managed conversations;
- transcript replay;
- governed-step isolation;
- approval continuation;
- transient context;
- request-scoped attachments;
- background responses and continuation tokens.

Opaque MAF session state is stored as JSON. Custom logic detects `conversationId` before deciding whether restoration is compatible. Serialization is bounded to five seconds and all non-cancellation failures currently collapse to `null`.

These are not all MAF bug workarounds. Most are CanDoItAll lifecycle and privacy policies.

## Handoff and Workflow Model

Handoff uses:

- `AgentWorkflowBuilder.CreateHandoffBuilderWith`;
- agent response update events;
- optional response events;
- handoff-only tool-call filtering;
- optional return-to-previous;
- configured routes and handoff instructions;
- `workflow.AsAIAgent(includeWorkflowOutputsInResponse: true)`;
- a custom `HandoffDepthGuardAgent`.

The depth guard's non-streaming path runs the streaming path and calls `updates.ToAgentResponse()`. The primary runtime also streams and independently calls `ToAgentResponse()`.

This creates two separate output projection concerns:

- MAF's internal workflow history merge;
- CanDoItAll's caller-visible streaming merge.

## File and Workspace Tools

Confirmed services include:

- `IWorkspaceFileService` / `WorkspaceFileService`;
- `IWorkspacePathResolutionService`;
- `IWorkspaceCommandExecutionService`;
- `IWorkspaceProcessHost`;
- `IWorkspaceDocumentMarkdownConverter`;
- `IWorkspaceImageOperationService`;
- `IWorkspaceArtifactToolService`;
- CanDoItAll.FileTools integration packages.

This is not MAF Harness file access. Harness APIs are only relevant if a separate branch path creates `HarnessAgent`.

## Hosting and A2A

Common hosting always calls `AddAgentFrameworkA2AHosting()`. The main adapter and hosting projects use matching 1.13 A2A preview builds today. The 1.15 upgrade must update both to the exact matching preview build and execute a hosted smoke test.

## Response and Usage Assembly

The runtime:

- snapshots streamed updates;
- merges all updates via MEAI `ToAgentResponse()`;
- extracts approval requests from the merged result;
- groups usage-bearing updates by response ID;
- counts distinct tool calls;
- tracks background continuation;
- builds required-finalizer responses from application-owned traces.

MAF 1.15 itself was tested upstream against MEAI 10.6.0, while this repository directly pins MEAI 10.8.0. The newer version should not be downgraded automatically, but message-merging tests must cover the actual resolved MEAI version.
