# Evidence

## Code evidence

- src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs:413-427 ProjectPartyAssignment stores NodeKey as a string
- src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs:668-683 configuration indexes NodeKey but does not enforce canonical node integrity
- src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs:4421-4500 SaveAssignmentAsync validates project/party, then upserts by ProjectId/PartyId/AssignmentKind/NodeKey without validating node existence or kind policy
- tests/CanDoItAll.Tests.Integration/ProjectPartyAssignmentIntegrationTests.cs:40-48 uses an arbitrary node key string ('work-item-alpha') as a valid node-scoped assignment input

## Root cause

The generic project-party bridge was built before a canonical node reference validator and actor-role policy existed.

## Architectural interpretation

This finding exists because the current code lets one concern occupy too many roles at once.
