# PRM-F06 — Approval policies, escalations, and governance gates

## Objective

Add approval gates, escalation rules, separation-of-duties constraints, and policy boundaries that align with future agent-rights enforcement while working for human and manual flows now.

## Priority and wave

- Priority: **High**
- Planned wave: **Wave 2**
- Depends on: **PRM-F03, PRM-F04, PRM-F05**

## Why this feature exists

This feature is part of the first process-management bundle because the user explicitly wants clear governance and responsibility around handoffs before deeper runtime automation.

## In scope

- Approval-required work pauses and resumes with auditable decisions.
- Escalation routes can target supervisory roles.
- Policy metadata is explicit and reviewable.
- Self-approval and conflicting-role combinations can be blocked unless an override path is explicitly configured.
- Future mapping to agent approval / collaboration rights remains possible.

## Non-goals

- Do not hide governance decisions inside runtime-only flags.
- Do not postpone conflict-of-duty modeling until after AI runtime integration.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Processes/ProcessPolicyModels.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessApprovalServices.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeServices.cs`
- `src/CanDoItAll.SharedKernel/ActivityStream.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessApprovalIntegrationTests.cs (new)`
