# Candidate Field Map Template

Codex must fill this before production movement.

| Route | Field | Current value source | Factory value source | Parity proof |
| --- | --- | --- | --- | --- |
| Subprocess | Run | snapshot.Run | TBD | TBD |
| Subprocess | Definition | snapshot.Definition | TBD | TBD |
| Subprocess | TechnicalAgentId | Guid.Empty | TBD | TBD |
| Subprocess | Cooperation | ProcessArtifactHandoff/ReadOnly | TBD | TBD |
| Workflow | TechnicalAgentId | Guid.Empty | TBD | TBD |
| Workflow | Cooperation | ProcessArtifactHandoff/ReadOnly | TBD | TBD |
| DirectAgent | TechnicalAgentId | bindingResult.TechnicalAgentId | TBD | TBD |
| DirectAgent | ManualRecoveryDirective | latest directive helper | TBD | TBD |
| DirectAgent | RecoveryExecutionRunId | recoverable/reused artifact recovery id | TBD | TBD |
| DirectAgent | Cooperation | ResolveProcessCooperationMetadata | TBD | TBD |

No row may remain TBD at Gate B.
