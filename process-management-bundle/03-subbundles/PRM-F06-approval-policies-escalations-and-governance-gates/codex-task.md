# Codex task — PRM-F06

Implement **Approval policies, escalations, and governance gates** inside the uploaded CanDoItAll solution.

## Constraints

- Treat `CanDoItAll.Modules.Processes` as the canonical owner for process-management behavior.
- Do not create a new durable actor registry; use CRM-HR bindings when actors are involved.
- Do not add direct compile-time dependency on the uploaded AgentFramework repo in the first process-management implementation.
- Keep all code comments in English.
- Preserve buildability for the current solution layout.

## Required outputs

- Code changes for this feature
- Matching tests
- Migration updates if persistence changes
- A short implementation note describing what changed and how it was verified

## Done definition

This task is done when:

- A process run can pause awaiting approval and resume with an auditable decision.
- Escalation routes can target a human party or supervisory role.
- Policy metadata is explicit and not hidden inside runtime-only configuration.
- Approval policies can prevent self-approval or conflicting role combinations unless an explicit override path is configured.
- The model can later map to agent external-call approvals and collaboration rights.

## Recommended first files to touch

- `src/CanDoItAll.Modules.Processes/ProcessPolicyModels.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessApprovalServices.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeServices.cs`
- `src/CanDoItAll.SharedKernel/ActivityStream.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessApprovalIntegrationTests.cs (new)`
