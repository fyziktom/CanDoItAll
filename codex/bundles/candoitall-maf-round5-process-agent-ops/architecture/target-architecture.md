# Target Architecture

## Principle

The process engine owns process truth. MAF agents are bounded executors. MAF sessions are conversation state, not process state.

## Runtime layers

```text
Process Engine
  - canonical state, transitions, assignments, artifacts, escalations

Agent Execution Coordinator
  - starts technical execution runs
  - applies structured output contracts
  - preserves structured output across continuations
  - enforces finalizer policy

Tool Governance
  - classifies tools
  - approval-wraps high-risk mutations
  - denies unknown processes_* mutations
  - records tool receipts

Recovery Engine
  - classifies failure
  - creates recovery decisions
  - creates rework packets
  - selects context strategy
  - controls retry budget/backoff

Proof Engine
  - captures proof receipts and fingerprints
  - determines reusable/invalidated proof
  - requests proof reruns

Escalation Control Plane
  - creates and resolves escalations/approvals
  - feeds UI queues
  - triggers rework/approval continuation

Process Workspace UI
  - monitoring, control, approvals, rework, evidence, audit
```

## Service decomposition

Extract these services from the current large partial classes:

- `IProcessAgentExecutionCoordinator`
- `IProcessRecoveryDecisionService`
- `IProcessReworkPacketService`
- `IProcessContextSelectionService`
- `IProcessProofFingerprintService`
- `IProcessEscalationService`
- `IProcessApprovalControlService`
- `IProcessOperatorCommandService`
- `IAgentToolPolicyClassifier`
- `IAgentFinalizerEnforcementService`
- `IAgentOutputValidationService`
