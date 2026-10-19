# Pure Process decision budget transitions

**28/28 focused Unit cases pass, with no skips.** This implements the arithmetic in
[the allocation design](../plan/P07-process-decision-budget.md). It does not implement
SQL admission, a durable shared ledger, runtime activation or a production producer.

Runtime now owns typed decision/allocation identities, immutable balances and grants,
exact settlement evidence references and pure reservation/settlement transitions.
New grants deduct from the same remaining work/replan allowance. Exact retries do not
consume or refund twice; changed identity/intent/evidence is refused. Closed settlement
charges actual work and releases unused reservation. Overruns remain recorded and block
new allocations while other reserved children can settle. Checked arithmetic and stored
balance validation reject negative, impossible and overflowing values explicitly.

Allocation history is bounded to 256 per lineage, including zero-consumption settlements.
This cap is still subject to the durable-owner capacity qualification before activation.
The transition uses one balance and exact allocation in constant work; it does not
enumerate child history, access storage, read clocks or interpret business receipts.
The application must verify exact closed owner evidence; these values are not trusted
HTTP/provider payloads. Persistence must atomically read the allocation and compare the
balance revision under the root fence before any concurrent-admission claim is valid.

## Validation and architecture

Built Runtime and Unit directly in standard Release with local sibling libraries and
serialized `/m:1`. Existing compiler/NuGet warnings remain. Discovered exactly 28 cases
with `FullyQualifiedName~ProcessDecisionBudgetTests`, then executed the same filter
with `--no-build --no-restore`. The command completed in 3.384 seconds; this is correctness
proof, not a throughput benchmark. Tests use no filesystem, database, clock, provider
or application host. They cover shared allowance, partial/zero settlement, history cap,
replay after later work, changed requests/evidence, foreign/unbacked allocations,
overruns, continued settlement, malformed state and extreme checked arithmetic.

CodeAnalytics `snap-20261019163221-d1ce2c09` → `snap-20261019165537-d1ce2c09`:
946→956 types, 8,689→8,735 members, 319→320 findings, unchanged one recognized service,
22 entities, 190 diagnostics and zero cycles. The new Info `COMPLEXITY-002` flags the
balance's 12 source members; they form one immutable budget/validation responsibility.
No interface, DI registration, project reference or production partial was added.
Existing workspace-load blocking diagnostics remain; actual builds pass. Architecture
gate passes only for the pure foundation, with persistence and runtime gates still open.

Full portability enforcement passes with **15,615 unchanged findings**, no baseline
write. Rendering register, documentation and diff checks also pass.

## Evidence and resources

Owned candidate: `.artifacts/concurrent-mutations/validation/c4-process-decision-budget`.
Its 9,417 source and 1,472 runtime inputs were frozen and verified unchanged before
release. Source SHA-256: `3B346B65D36CE36B36E7F45C8A0E7CB89FEDC77E5EAD133045D12658C5CE268A`.
Runtime SHA-256: `022FFDD5418610546637CE44666CF2EC3D14C8587551D4B52E3559522F9901C8`.
Compact proof under ignored `proof/2026-10-19-planning-authority/c4-process-decision-budget.json`:
`2D61253232BF85A8069BC149BBEA109A65C8F2BB0CED3EC27CD98CB8A7231F04`.

Ownership preceded creation. The observer covered the entire command and reconciliation
found zero remaining observed processes and zero database delta. Three unattributed TEMP
entries were preserved. No database fixture was created or required by these tests;
the existing isolated server remains for later integration. Earlier cleanup blockers
were not retried. SPA remains on its isolated branch.
