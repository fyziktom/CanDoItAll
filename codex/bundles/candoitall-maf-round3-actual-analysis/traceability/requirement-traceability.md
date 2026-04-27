# Requirement Traceability

| Requirement | Primary subbundle | Key tests expected | Implementation proof |
|---|---|---|---|
| R00 | 00-secret | secret scan regression | `SecretScanningTests`; tracked `git grep` scan returned no matches; full worktree scan excluding generated dependency folders returned no matches. |
| R01 | 01-process-tool-policy | process mutation classification; post-finalizer process mutation violation | `AgentToolInvocationPolicyTests`, `AgentFinalizerPolicyTests`, `MafAgentRuntimeTests`; process mutation tools are in `AgentToolInvocationPolicy.RegisteredTools`. |
| R02 | 02-recovery-taxonomy | failure classification tests | `AgentRecoveryModelsTests`; `AgentRecoveryDecision`, `AgentRecoveryMode`, and `AgentFailureCategory` implemented in `AgentRecoveryModels.cs`. |
| R03 | 02/04-rework-packet | QA rejection/manual rerun packet tests | `AgentRecoveryModelsTests`, `ProcessRunAutomationDispatchServiceTests`; dispatch and manual rerun paths create `AgentReworkPacket` journal events. |
| R04 | 03-context | session strategy tests | `AgentRecoveryModelsTests`; context strategy is represented by `AgentRecoverySessionStrategy` and decisions for format repair, fresh retry, provider fallback, approval continuation, and rework continuation. |
| R05 | 04-QA-loop | QA -> repair -> QA recheck flow | `AgentRecoveryModelsTests`, `ProcessRunAutomationDispatchServiceTests`; packet rendering includes findings, artifact refs, minimal actions, prohibited actions, and proof requirements. |
| R06 | 05-proof-fingerprint | proof reuse/invalidation tests | `AgentRecoveryModelsTests`; `AgentProofFingerprintService` hashes commands, file inputs, artifacts, environment, status, and tool version. |
| R07 | 06-ledger | repeated failure/backoff/escalation tests | `AgentRecoveryModelsTests`; `AgentRecoveryLedger` records attempt state, backoff, provider fallback budget, and repeated-failure escalation. |
| R08 | 07-provider-approval | provider approval matrix proof tests | `ProviderFeatureMatrixTests`, `MafAgentRuntimeTests`; OpenAI/Azure OpenAI Chat Completions support approval-required function tools when tools are enabled. |
| R09 | 08-guidance-provider | domain-string static regression + provider selection tests | `AgentRuntimeHardeningStaticRegressionTests`, `ProcessRunAutomationDispatchServiceTests`; domain guidance is selected through `IProcessAutomationRecoveryGuidanceProvider`. |
| R10 | 10-tests-docs | docs truthfulness verification | `docs/agent-recovery-stabilization.md`, `docs/secure-configuration.md`, and this execution report list commands that were actually run and distinguish focused proof from full-suite residual failures. |
