# Evidence

## Code evidence

- src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs:62-119 defines a generic project-party assignment contract with optional NodeKey
- src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs:413-427 defines ProjectPartyAssignment with project-, opportunity-, phase-, and node-scoped fields
- src/CanDoItAll.Modules.Resources/ResourceModels.cs:84-92, src/CanDoItAll.Modules.Validation/ValidationModels.cs:50-58, and src/CanDoItAll.Modules.TestLab/TestLabModels.cs:18-25 store responsibility directly inside module aggregates
- tests/CanDoItAll.Tests.Integration/CrmHrCrossModuleIntegrationTests.cs:186-255 demonstrate positive round-trip behavior across these modules, but not a canonical ownership rule

## Root cause

CRM/HR integration correctly broadened project actor semantics, but did so via multiple local representations before one cross-module actor-assignment model was fixed.

## Architectural interpretation

This finding exists because the current code lets one concern occupy too many roles at once.
