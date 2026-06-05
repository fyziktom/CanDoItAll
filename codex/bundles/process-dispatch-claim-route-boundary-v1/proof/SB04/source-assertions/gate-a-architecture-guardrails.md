# SB04 Source Assertions

- Gate A added two focused architecture tests in `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`.
- `Process_dispatch_claim_route_gate_a_SB04_INV_001_records_live_inventory_and_blocks_core_driver_or_viewport_drift` asserts live route/concurrency inventory entries, MAF project dependency isolation, absence of Process Core directories, absence of production driver API tokens in Processes module source, and no prohibited proof paths.
- `Process_dispatch_claim_route_gate_a_SB04_INV_002_rejects_placeholder_or_stale_inventories` rejects the seeded placeholder inventory state and requires live SB02/SB03 source markers.
- Production dispatch source remains unchanged in SB04.
- Test platform: xUnit 2 through VSTest, detected from `repo://tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj` with `Microsoft.NET.Test.Sdk`, `xunit`, and `xunit.runner.visualstudio`.
