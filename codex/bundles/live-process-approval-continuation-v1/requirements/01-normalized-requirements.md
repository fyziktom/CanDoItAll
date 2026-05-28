# Normalized Requirements

| ID | Requirement | Observable success |
| --- | --- | --- |
| R001 | Live Processes must not label non-approval escalations as `Approve` or `Deny`. | Blocked and failed step cards show a rework-oriented primary action. |
| R002 | Live Processes must request governed step rework directly for step-scoped blocked/failed recovery escalations. | The action calls `IProcessEscalationService.RequestReworkAsync` and refreshes the live snapshot. |
| R003 | Live Processes must continue actual operator approvals directly when source execution approval ids are present. | The action calls `IAgentFrameworkWorkspaceService.ContinueExecutionRunAsync` before recording the approval decision. |
| R004 | Manager chat must remain explicit discussion, not the hidden continuation mechanism for quick actions. | Quick unblock buttons do not call manager chat for the observed blocked-step escalation. |
| R005 | The repair must be covered by targeted regression proof. | Tests assert blocked-step action policy and approval action policy. |
