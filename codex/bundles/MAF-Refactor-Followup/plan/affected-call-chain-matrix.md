# Affected call-chain matrix

| Chain | Critical files | Required proof |
|---|---|---|
| Floating send/context | AgentChatPanel, AgentChatExecutionOrchestrator, AgentTurnContextCaptureService, AgentTurnContextMetadata | immutable turn + authority, transition, no UI grant |
| Execution admission | AgentFrameworkWorkspaceExecutionService.ExecutionRuns | governance snapshot validation and persistence |
| Runtime composition | MafAgentExecutionAdapter, MafAgentRuntime, MafRuntimeAgentFactory, RuntimeCapabilityComposer | same authority/scope in planner and policy |
| Workspace tools | WorkspaceRuntimeServices, WorkspaceExecutionScope, ToolCapabilityBuilder, runtime tool providers | one bundle, full identity, no mixed services |
| Recovery/script | MafStreamingTurnExecutor, ProcessAgentExecutionOutcomeRecoveryPolicy, MafScriptPolicyInspectionService | exact run scope and artifact evidence |
| Continuation | AgentTurnContextLeaseRegistry, MafAgentContinuationAdapter, MafApprovalContinuationDriver | original context/authority and exact decisions |
| Session state | MafRuntimeSessionBuilder, PersistenceDriver, StateAdapter, CompatibilityPolicy, EnvelopeModels | v0/v1/v2 matrix and native payload inspection |
| Tool governance | MafRuntimeAgentFactory, AgentToolInvocationPolicy, WorkspaceExecutionAuditContext, Processes policies | injected neutral pipeline, explicit facts |
| Lightweight LLM | Llm.Abstractions, Llm.ProviderRuntime, Workflow LLM invoker/registration, provider drivers | no agent path, bounded failures/retry/usage |
| Workspace lifetime | AgentFrameworkWorkspaceFactory, AgentFrameworkWorkspaceService, CurrentProfile workspace | one process host, dispose once, profile switch |
| Approval UI/API | AgentChatPanel, AgentApprovalDecisionRequestMapper, AgentsApi/EventsApi | mixed per-proposal decisions and compatibility endpoint |
