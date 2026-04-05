# 04-harden-node-scope-and-assignment-boundaries

## Status

- `Completed`

## Objective

Make canonical node scope explicit for assignments, agents, and future node-scoped roles without depending on projection rows.

## Covered Inputs

- `PW6-010`
- `PW6-009`

## Prerequisites

- SB01 and SB03 complete.
- Decide whether the persisted storage format remains string NodeKey or graduates to a stronger internal owned type.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectPartyIntegrationContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectNodeScopeBridge.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\CrmHrBusinessModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\CrmHrServices.cs`

## Evidence Focus

- `src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs:121-149`
- `src/CanDoItAll.Modules.Workbench/ProjectNodeScopeBridge.cs:8-68`
- `src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs:413-428`
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs:4888-4933; 4994-5002`

## Deliverables

- Canonical node scope resolution that cannot accidentally target projection-only nodes.
- Assignment persistence and validation path fully hidden behind typed node references and registry capabilities.
- Clear policy for which roles may ever be node-scoped.

## Dependency Impact

- Prevents CRM/HR and future plugins from attaching people/agents/partners to the wrong kinds of nodes.
- Protects the system when projection contributors become richer.

## Validation Depth

- Integration tests for allowed and forbidden node-scoped assignments.
- Tests ensuring projection-only nodes cannot be assignment targets.
- Tests ensuring foreign-project nodes are rejected.

## Implementation Steps

- Refactor scope resolution to distinguish canonical carrier nodes from assembled projection-only nodes.
- Apply node capability validation to every node-scoped role, not only two special cases.
- If raw string storage remains, keep it fully encapsulated inside persistence and never expose it as the semantic contract.

## Do Not Do

- Do not make projection rows legal assignment targets just because they have a NodeKey.
- Do not scatter new node-role validation rules into multiple services.

## Acceptance Checklist

- [ ] Assignments can target only canonical nodes with the required assignable capability.
- [ ] All node-scoped roles are validated through one semantic path.
- [ ] Foreign project nodes and projection-only nodes are rejected.

## Proof Required

- Assignment integration tests.
- Capability matrix evidence.
- Updated contract review.

## Browser Validation Logging

- Capture node assignment UI flows if they visibly change.

## Progression Gate

- Do not start SB05 until node scope semantics are explicit and tested.

## Suggested Agent Prompt

Implement SB04 so that typed node references remain the public boundary, but canonical scope resolution and role validation become explicit and projection-safe.
