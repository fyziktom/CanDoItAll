# High-risk file and ownership map

| File/area | Risk | Safe editing rule |
|---|---|---|
| AgentTurnContextCaptureService.cs | turn snapshot/authority mismatch | preserve one capture; no provider/tool work here |
| AgentExecutionAuthorityComposition.cs | privilege grant | source provider must validate durable state and post-await generation |
| AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs | central orchestration hotspot | extract cohesive validators/builders; do not add more process/string branches |
| WorkspaceExecutionScope.cs | cross-profile/cross-platform identity | version identity semantics and add fixtures before changing equality |
| WorkspaceRuntimeServices.cs | process/file service lifetime | one owner, one process host, idempotency is not ownership |
| AgentFrameworkWorkspaceFactory.cs | parallel composition root | switch graph atomically and prove disposal |
| MafRuntimeAgentFactory.cs | tool policy and scope leakage | inject policy; per-build services; keep MAF-specific mapping only |
| MafStreamingTurnExecutor.cs | provider/finalizer/recovery | recovery receives run bundle; no product semantics |
| MafRuntimeSessionBuilder.cs | data-loss/continuation risk | fixture-first; judge envelope before unwrap; no silent replay |
| RuntimeStateEnvelopeModels.cs | persisted schema | additive version/migration; write newest only |
| MafApprovalContinuationDriver.cs | ephemeral binding cache | durable state is authority; bounded cache only |
| AgentChatPanel.razor.cs | user approval correctness | proposal-specific state and stale-run guards |
| ProviderBackedLlmInvocationAdapter.cs | provider reliability/security | typed failures, bounded retry, no agent fallback |
| MafWorkflowLlmComponentInvoker.cs | wrong physical owner | move without altering workflow semantic validation/usage |
| ProjectStructureAgentRuntimeToolProvider.cs | duplicate authorization | preserve domain invariants; remove independent grants after canonical cutover |
