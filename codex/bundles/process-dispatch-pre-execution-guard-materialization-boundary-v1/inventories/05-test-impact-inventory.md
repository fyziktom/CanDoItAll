# Test Impact Inventory

Codex must fill exact test names in SB02.

Expected test areas:

- `ProcessRunAutomationDispatchServiceTests`
- `ProcessAgentExecutionBoundaryArchitectureTests`
- candidate hydration/factory route parity tests
- missing upstream artifact materialization tests
- process journal duplicate tests
- transition request field tests
- recovery directive/rerun request tests

Required test slices:

- no missing upstream artifacts => dispatch continues
- missing upstream artifacts without target => downstream blocked and journaled, no rerun
- missing upstream artifacts with target => downstream blocked, journaled, rerun requested
- duplicate fingerprint => no duplicate rerun
- database requirement failure => correct block/fail transition
