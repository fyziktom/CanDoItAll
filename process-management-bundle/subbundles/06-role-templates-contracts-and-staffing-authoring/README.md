# 06 Role Templates, Contracts, And Staffing Authoring

## Status

- `Completed`

## Objective

- Implement the role-first authoring model by connecting process roles, step contracts, reusable role templates, staffing intent, fallback routes, and durable snapshots without duplicating CRM-HR truth.

## Covered Inputs

- `REQ-002`
- `REQ-006`
- `REQ-008`
- Raw notes `N05`, `N06`, and `N07`
- Legacy features `PRM-F03`, `PRM-F04`, and `PRM-F16`

## Prerequisites

- `05-process-definition-lifecycle-and-governance-model`

## Exact Source References

- `C:\repositories\CanDoItAll\process-management-bundle\03-subbundles\PRM-F03-actor-roles-responsibilities-and-crmhr-bindings\README.md`
- `C:\repositories\CanDoItAll\process-management-bundle\03-subbundles\PRM-F04-step-contracts-inputs-outputs-and-evidence\README.md`
- `C:\repositories\CanDoItAll\process-management-bundle\03-subbundles\PRM-F16-role-and-agent-templates-staffing-briefs-and-sourcing-handoffs\README.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\CrmHrBusinessModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\CrmHrServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages`

## Deliverables

- Process-role model based on role requirement, not fixed executor.
- Reusable CRM-HR role-template and staffing-template usage model with version snapshots.
- Step-contract model for inputs, outputs, evidence, and trust-sensitive artifact expectations.
- Eligibility, fallback, and rebinding semantics that preserve auditability.

## Dependency Impact

- Runtime assignment, approvals, work briefs, and future AI bridge work all depend on this role-first model.
- If this subbundle leaks executor-first logic, every downstream phase becomes harder to stabilize.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Implement role requirements and role-binding snapshot strategy.
2. Connect process authoring to CRM-HR role templates and staffing intent.
3. Implement structured step contracts for inputs, outputs, evidence, and artifact expectations.
4. Add fallback, backup, and rebind semantics without rewriting process history.

## Scope Exceptions

- Final runtime assignment resolution happens in phase 02, but the authoring and snapshot model must be complete now.

## Do Not Do

- Do not store process roles as free-text only.
- Do not bypass CRM-HR by creating a process-local business role catalog.
- Do not model AI participation as the only or default executor type.

## Acceptance Checklist

- A process role can exist before a concrete assignee is chosen.
- Role templates and staffing intent are snapshot-aware.
- Step contracts and evidence expectations are explicit and queryable.
- Rebinding and fallback behavior preserves prior-run auditability.

## Proof Required

- Integration tests against CRM-HR-backed templates and identity references.
- Tests for template snapshotting and later template edits not rewriting history.
- Proof that step-contract and artifact-expectation data remains typed and queryable.

## Browser Validation Logging

- Route:
  CRM-HR role-template and staffing surfaces touched by this work
- Route:
  process authoring role-editor surfaces when available
- Viewport:
  `1920x1080`
- Evidence:
  component-first role and contract editing proof if browser-visible UI changes are included

## Progression Gate

- Downstream runtime work may start only when the role-first staffing model is stable and no business-role truth was duplicated outside CRM-HR.

## Suggested Agent Prompt

```text
Implement only the role-first authoring slice. Connect process roles to CRM-HR templates and staffing intent, keep step contracts typed, and preserve snapshot history when templates or assignees change later.
```
