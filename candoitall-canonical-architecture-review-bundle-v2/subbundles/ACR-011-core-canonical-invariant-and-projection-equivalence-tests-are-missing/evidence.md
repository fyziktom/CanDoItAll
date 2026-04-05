# Evidence

## Code evidence

- tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs contains workbench mutation/projection tests, but no visible reparent-cycle rejection coverage
- tests/CanDoItAll.Tests.Integration/ProjectPartyAssignmentIntegrationTests.cs:23-66 covers positive project/node assignment persistence, but not orphan node keys, mismatched projects, or illegal node-kind roles
- tests/CanDoItAll.Tests.Components/CrossModuleResponsiblePartyPageTests.cs:29-53 and 68-116 cover positive round-trips, but not ownership divergence or canonical-source enforcement

## Root cause

Existing tests grew around current behavior, not around the future canonical contracts now needed for stabilization work.

## Architectural interpretation

This finding exists because the current code lets one concern occupy too many roles at once.
