# Normalized Requirements

## RQ-01 Canonical Owner

- Node-scoped participant, meeting, and work-item party links must use `ProjectPartyAssignment` as the canonical owner.

## RQ-02 Canonical Read Path

- The structure-page party editor must initialize its selected state from canonical assignment rows, not from workbench metadata.

## RQ-03 Derived Projection Only

- Workbench metadata for participant, meeting, and work-item party fields may remain for preview or labeling, but it must be updated as a derived projection and must not drive editor truth.

## RQ-04 Lifecycle Reconciliation

- Deleting a node or descendant subtree must remove the corresponding node-scoped assignments.
- Moving descendants into another project must move the corresponding node-scoped assignments to the new project.

## RQ-05 Boundary Discipline

- Workbench must use project-facing contracts for canonical assignment lifecycle changes instead of manipulating CRM/HR persistence types directly.

## RQ-06 Test Coverage

- Integration coverage must protect delete and subtree-transfer assignment reconciliation.
- Component or browser-visible coverage must prove the structure-page editor still behaves correctly after the canonical read-path repair.

## RQ-07 Bundle Closure

- The new bundle must pass prepared-stage validation before execution and completed-stage validation before final closure.
- The final execution report must include the post-fix architecture review result and honest residual risks.
