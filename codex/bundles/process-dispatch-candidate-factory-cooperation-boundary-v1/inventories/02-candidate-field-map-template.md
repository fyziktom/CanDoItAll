# Candidate Field Map

Filled before production movement in SB02.

| Route | Field | Current value source | Factory value source | Parity proof |
| --- | --- | --- | --- | --- |
| Subprocess | Run | `snapshot.Run` | `ProcessDispatchCandidateAssemblyContext.Run` | `ProcessDispatchCandidateFactory.CreateSubprocessCandidate` field parity test |
| Subprocess | Definition | `snapshot.Definition` | `ProcessDispatchCandidateAssemblyContext.Definition` | `ProcessDispatchCandidateFactory.CreateSubprocessCandidate` field parity test |
| Subprocess | StepRun | current `stepRun` from `snapshot.DispatchableSteps` | `ProcessDispatchCandidateAssemblyContext.StepRun` | `ProcessDispatchCandidateFactory.CreateSubprocessCandidate` field parity test |
| Subprocess | StepDefinition | `currentStepDefinition` from `snapshot.ReadyStepDefinitionsById` | `ProcessDispatchCandidateAssemblyContext.StepDefinition` | `ProcessDispatchCandidateFactory.CreateSubprocessCandidate` field parity test |
| Subprocess | WorkBrief | `snapshot.WorkBriefsByStepRunId.GetValueOrDefault(stepRun.Id)` | `ProcessDispatchCandidateAssemblyContext.WorkBrief` | `ProcessDispatchCandidateFactory.CreateSubprocessCandidate` field parity test |
| Subprocess | TechnicalAgentId | `Guid.Empty` | `Guid.Empty` in subprocess factory method | `ProcessDispatchCandidateFactory.CreateSubprocessCandidate` field parity test |
| Subprocess | ExpectedArtifacts | `LoadExpectedArtifactsAsync` result | `ProcessDispatchCandidateAssemblyContext.ExpectedArtifacts` | `ProcessDispatchCandidateFactory.CreateSubprocessCandidate` field parity test |
| Subprocess | RecordedArtifactExpectationIds | current step artifacts with expectation ids | `ProcessDispatchCandidateAssemblyContext.RecordedArtifactExpectationIds` | `ProcessDispatchCandidateFactory.CreateSubprocessCandidate` field parity test |
| Subprocess | ArtifactInputs | `PrepareArtifactInputsForPrompt(BuildResolvedArtifactInputs(...))` | `ProcessDispatchCandidateAssemblyContext.ArtifactInputs` | `ProcessDispatchCandidateFactory.CreateSubprocessCandidate` field parity test |
| Subprocess | ExternalReferenceKeys | `snapshot.ExternalReferenceKeys` | `ProcessDispatchCandidateAssemblyContext.ExternalReferenceKeys` | `ProcessDispatchCandidateFactory.CreateSubprocessCandidate` field parity test |
| Subprocess | ChatSessionId | `null` | `null` in subprocess factory method | `ProcessDispatchCandidateFactory.CreateSubprocessCandidate` field parity test |
| Subprocess | RecoveryExecutionRunId | `null` | `null` in subprocess factory method | `ProcessDispatchCandidateFactory.CreateSubprocessCandidate` field parity test |
| Subprocess | ManualRecoveryDirective | `string.Empty` | `string.Empty` in subprocess factory method | `ProcessDispatchCandidateFactory.CreateSubprocessCandidate` field parity test |
| Subprocess | BranchOutcomes | `branchContext.BranchOutcomes` | `ProcessDispatchCandidateAssemblyContext.BranchOutcomes` | `ProcessDispatchCandidateFactory.CreateSubprocessCandidate` field parity test |
| Subprocess | RequiresExplicitBranchOutcomeSelection | `branchContext.RequiresExplicitBranchOutcomeSelection` | `ProcessDispatchCandidateAssemblyContext.RequiresExplicitBranchOutcomeSelection` | `ProcessDispatchCandidateFactory.CreateSubprocessCandidate` field parity test |
| Subprocess | Cooperation | `ProcessArtifactHandoff` / `ReadOnly` / subprocess summary | subprocess factory method static metadata | `ProcessDispatchCandidateFactory.CreateSubprocessCandidate` field parity test |
| Workflow | Run through ExternalReferenceKeys | same common sources as subprocess | same common assembly context fields | `ProcessDispatchCandidateFactory.CreateWorkflowCandidate` field parity test |
| Workflow | TechnicalAgentId | `Guid.Empty` | `Guid.Empty` in workflow factory method | `ProcessDispatchCandidateFactory.CreateWorkflowCandidate` field parity test |
| Workflow | ChatSessionId | `null` | `null` in workflow factory method | `ProcessDispatchCandidateFactory.CreateWorkflowCandidate` field parity test |
| Workflow | RecoveryExecutionRunId | `null` | `null` in workflow factory method | `ProcessDispatchCandidateFactory.CreateWorkflowCandidate` field parity test |
| Workflow | ManualRecoveryDirective | `string.Empty` | `string.Empty` in workflow factory method | `ProcessDispatchCandidateFactory.CreateWorkflowCandidate` field parity test |
| Workflow | Branch fields | `branchContext` | `ProcessDispatchCandidateAssemblyContext` branch fields | `ProcessDispatchCandidateFactory.CreateWorkflowCandidate` field parity test |
| Workflow | Cooperation | `ProcessArtifactHandoff` / `ReadOnly` / workflow summary | workflow factory method static metadata | `ProcessDispatchCandidateFactory.CreateWorkflowCandidate` field parity test |
| DirectAgent | Run through ExternalReferenceKeys | same common sources as subprocess | same common assembly context fields | `ProcessDispatchCandidateFactory.CreateDirectAgentCandidate` field parity test |
| DirectAgent | TechnicalAgentId | `bindingResult.TechnicalAgentId` after binding side effect resolution | `technicalAgentId` parameter | `ProcessDispatchCandidateFactory.CreateDirectAgentCandidate` field parity test |
| DirectAgent | ChatSessionId | local `reusableChatSessionId` currently `null` | `reusableChatSessionId` parameter | `ProcessDispatchCandidateFactory.CreateDirectAgentCandidate` field parity test |
| DirectAgent | ManualRecoveryDirective | `LoadLatestManualRecoveryDirectiveAsync` result | `manualRecoveryDirective` parameter | `ProcessDispatchCandidateFactory.CreateDirectAgentCandidate` field parity test |
| DirectAgent | RecoveryExecutionRunId | `ResolveRecoverableExecutionRunId` or artifact-recovery reuse id | `recoveryExecutionRunId` parameter | `ProcessDispatchCandidateFactory.CreateDirectAgentCandidate` field parity test |
| DirectAgent | Branch fields | `branchContext` | `ProcessDispatchCandidateAssemblyContext` branch fields | `ProcessDispatchCandidateFactory.CreateDirectAgentCandidate` field parity test |
| DirectAgent | Cooperation | `ResolveProcessCooperationMetadata(...)` | `cooperationMetadata` parameter resolved outside factory | `ProcessDispatchCandidateFactory.CreateDirectAgentCandidate` field parity test |

No row may remain TBD at Gate B.
