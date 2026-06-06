# Documentation-only Driver Readiness Map

This bundle still must not create production driver APIs.

## Why this matters for future drivers

Future drivers should eventually plug into route stages without forcing process core to know `.NET`, Rust, browser, Office, business-analysis, or agent-improvement details.

## Vocabulary to document only

| Route area | Future driver-relevant vocabulary | Possible future driver family |
| --- | --- | --- |
| Direct agent execution | `AgentExecutionEvidence`, `ToolReceiptFacts`, `CompletionEvidence` | Generic process execution driver |
| Database requirement | `RuntimeRequirementEvidence` | Runtime environment driver |
| Upstream materialization | `ArtifactMaterializationIntent`, `EvidenceGap` | Artifact/evidence driver |
| Subprocess | `DelegatedProcessEvidence`, `CapabilityGapEvidence` | Process composition driver |
| Workflow | `WorkflowDelegationEvidence` | Workflow driver |
| Browser proof | `BrowserRuntimeEvidence`, `VisualProofEvidence` | Browser proof driver |
| Finalizer transition | `FinalizerDecisionEvidence` | Governance/finalizer driver |

## Explicitly forbidden in this bundle

- `IProcessDriverPack`
- `IProcessDriverRegistry`
- `ProcessDriverRegistry`
- `CanDoItAll.Processes.DriverPacks.*`
- any package or project named `DriverPack`
