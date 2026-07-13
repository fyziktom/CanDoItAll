# Findings summary

| ID | Severity | Area | Short fix |
|---|---|---|---|
| F01 | Critical | Operator diagnostics / observation correlation | Query observations by exact (ProcessRunId, StepInstanceId), persist structured final output into ResultSummary, and fall back to StrategyResultReceipt diagnostics when AgentFramework observation is unavailable. |
| F02 | Critical | Execution observation query | Extend ProcessExecutionObservationQuery with StepInstanceIds or StepSelectors and query ExecutionRunQuery with ProcessStepId where possible. Use run-level fallback only for live dashboards, not for operator actions. |
| F03 | Critical | Subprocess orchestration | Make subprocess launch/wait/handoff runtime-owned. Keep the tool only as a compatibility/manual path. Do not ask normal agents to launch controlled child processes for StepKind=Subprocess. |
| F04 | Critical | Subprocess child-to-parent artifact bridge | Introduce ParentSubprocessArtifactBridge that validates child terminal branch/step/artifact and writes a parent managed artifact for the exact parent produced slot. |
| F05 | High | Artifact finalization / ledger consistency | Change BuildArtifactLedgerEvents to take StrategyResultEnvelope appliedResult and only write ledger events for the applied produced artifacts and completed/evidence-valid result policy. |
| F06 | High | Runtime contract prompt | Add semantic artifact slot descriptors and render them into prompts, diagnostics, operator hints and rework packets. |
| F07 | High | Produced artifact identity / content grounding | Tie produced artifact refs to deterministic managed artifact IDs and content hashes after materialization/readback. Store primary managed ref in the artifact receipt. |
| F08 | High | Capability/tool preflight | Add exact composed-tool preflight before claim/dispatch. If required tool missing, block with a concrete runtime diagnostic before invoking the agent. |
| F09 | High | Template contracts | Disable manual skip for this step or require an explicit AlreadyExistingSkeletonProof output contract. Add SubprocessContract with accepted and no-go child outputs. |
| F10 | Medium | Rework loop quality | Create a BlockedStepPacket and include it in operator action and rework prompt. Do not allow blind retry when diagnostic is missing. |
| F11 | Medium | Template size and cognitive load | Move hard gates to typed CompletionGates, RequiredReceipts, RequiredPaths, RequiredFileContentChecks, BranchRules and SubprocessContracts. Keep prose short as explanatory guidance. |
| F12 | Medium | Code architecture / maintainability | During these fixes split by responsibility: observation correlation, blocked packet builder, artifact descriptor resolver, parent subprocess bridge, completion gate validator, result summary persistence. |
