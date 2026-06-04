# Proof Strategy

Every migrated path must prove:

- same external-reference key as before,
- same projection lineage source kind and source execution run id,
- same trust status and sensitivity,
- same managed storage path intent/relative path hint where applicable,
- same hard/soft failure behavior,
- same candidate state updates,
- same required-artifact satisfaction results.

Required test classes/slices:

- `ProcessRunAutomationDispatchServiceTests` focused artifact/projection tests.
- `ProcessAgentExecutionBoundaryArchitectureTests` or equivalent static guard tests.
- Full solution build.
- No Process Core/driver-pack scan.
- No prohibited viewport proof scan.
