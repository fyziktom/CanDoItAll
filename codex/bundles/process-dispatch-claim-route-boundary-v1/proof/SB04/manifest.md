# SB04 Proof Manifest

- Subbundle: SB04 - Gate A architecture guardrails.
- Status: Completed.
- Owned requirements: RQ-002, RQ-004, RQ-013, RQ-014.
- Owned raw notes: RN-002, RN-003, RN-004.
- Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.md`.

## Changed File Manifest

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | `FDB8FA969108D223B1C24599D1BF7E7C475B6243ED77499C308860B99C27240B` | `9235EF2103231CE81B30A7057B34CA45763201BF27DD44DAC0D25B006D674A4F` |
| `bundle://inventories/02-current-dispatch-route-map.md` | `586FFF0CCA8334E85E5AE9172B37166AA3554D19514B9DCF36E5703F0707AD17` | `1A335A346DC81923CC2F00729F8A86C5598B65D18BB9ED4E13D87A834F52FED4` |
| `bundle://inventories/03-concurrency-rule-inventory.md` | `B7ACAABF6A7138DDAA80E2AB993EBDD832D00F8A84BB2D507F7554F35359F072` | `39BC90B03EB88686A9ECB8AE26A13188C5F2C7BD089CE658EBAC05DD1F8A7408` |

## Command Transcripts

- Test platform detection inputs: `repo://global.json`, `repo://tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`.
- Failing-first live inventory gate: `bundle://proof/SB04/transcripts/sb04-failing-first-live-inventory-gate.txt`.
- Supporting placeholder check: `bundle://proof/SB04/transcripts/sb04-failing-first-placeholder-inventory-check.txt`.
- Passing architecture tests: `bundle://proof/SB04/transcripts/sb04-new-architecture-tests.txt`.
- Production anti-stub and scope scan: `bundle://proof/SB04/transcripts/sb04-production-anti-stub-and-scope-scan.txt`.

## Failing-First Proof

- `bundle://proof/SB04/transcripts/sb04-failing-first-live-inventory-gate.txt` exits non-zero against `HEAD` inventory and contains `SB04_INV_001` and `SB04_INV_002`.

## Passing Proof

- `bundle://proof/SB04/transcripts/sb04-new-architecture-tests.txt` passes the current Gate A tests.
- Test name: `CanDoItAll.Tests.Unit.ProcessAgentExecutionBoundaryArchitectureTests.Process_dispatch_claim_route_gate_a_SB04_INV_001_records_live_inventory_and_blocks_core_driver_or_viewport_drift`
- Test name: `CanDoItAll.Tests.Unit.ProcessAgentExecutionBoundaryArchitectureTests.Process_dispatch_claim_route_gate_a_SB04_INV_002_rejects_placeholder_or_stale_inventories`

## Source Assertions

- `bundle://proof/SB04/source-assertions/gate-a-architecture-guardrails.md`.

## Anti-Stub Audit

- `bundle://proof/SB04/transcripts/sb04-production-anti-stub-and-scope-scan.txt` states no production dispatch stubs and no Process Core, driver API, or UI drift.

## Browser Proof

- N/A. Runtime/service refactor only; no UI files changed.
