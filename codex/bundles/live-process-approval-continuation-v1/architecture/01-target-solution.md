# Target Solution

- Add a small typed Live Processes escalation action policy that classifies each escalation as approval decision, rework request, resolve-only, or manager-message.
- Extend the live escalation projection with source execution and approval ids so approval decisions can use the same direct continuation boundary as Process Workspace.
- Update `LiveProcessesDashboard.razor` to render action labels from the policy and dispatch actions to `IAgentFrameworkWorkspaceService` or `IProcessEscalationService` instead of always sending a manager-chat prompt.
- Keep manager chat available as an explicit discussion path; do not use it as the automatic continuation path for a blocked-step card.
- Add focused tests for action classification. Avoid broad refactors or process-runtime behavior changes outside the action path.
