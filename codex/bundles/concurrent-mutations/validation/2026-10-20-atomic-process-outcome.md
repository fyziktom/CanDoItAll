# Atomic Process child completion validation

Status: Pass for 100 distinct selected behaviors across the documented fixture-only
correction boundaries. This is a persistence prerequisite for the selected
Process producer, not production activation or bundle closure.

## Scope and design

[Design and risk review](../plan/P07-atomic-process-outcome.md).
Four existing Runtime/Persistence owners implement optional typed child completion.
The existing root transaction settles the exact allocation and owner work revision,
closes the final child, advances its generation and commits the terminal runtime step,
claim release and strategy result together. No project reference, migration, service,
runtime partial or provider boundary is added.

Receipt v4 is emitted only for explicit completion; null completion retains canonical
v1-v3 shape. One literal v1 closure golden and existing persisted v2/v3 replay cases
cover compatibility. This does not claim literal golden fixtures for all legacy versions.
Old workers cannot read v4; mixed-version deployment remains gated before activation.

The final closure validates at most 64 retained children. No provider work, catalog
acquisition, whole launch decoding or global lock is introduced into the transaction.
Independent-root progress uses the existing root fence.

## Selection and actual commands

Candidate root: `.artifacts/concurrent-mutations/validation/c4-process-atomic-outcome`.
Standard Release builds directly refresh Runtime and Persistence, plus Unit and Integration
test assemblies. No source/package mode or custom output configuration changes.

| Lane | Expected discovery | Scope |
| --- | ---: | --- |
| generation-codec | 55 | 26 new transition/receipt cases plus 29 existing generation cases |
| atomic-outcome | 18 | SQL terminal states, exact accounting, rollback, lost acknowledgement, replay, corrupt counters |
| legacy-decision | 16 | Existing v1 replay, rollback, quiescence, corruption, cancellation accounting and independent roots |
| material-scope | 11 | Existing material/shared-scope v2/v3 receipt, replay and refresh contracts |

Exact filters and bounded command transcripts are retained with the proof. Discovery must
match each expected count before execution. The SQL fixture arranges a genuine persisted
claim and strategy result, then uses the actual unit of work; it does not yet execute the
production Agent adapter or prove its outcome evidence reader.

## Initial candidate and exact correction

All 100 cases executed: 82 passed; all 18 new SQL cases failed because their arranged
strategy result used a descriptive label instead of the existing required SHA-256 digest.
The failures were retained before changing source. Only that literal becomes a valid
sha256-prefixed 64-character digest; no production behavior, assertions or selected test count changes.
The 18 affected cases repeat after rebuilding Integration. The unchanged 55 Unit and
27 existing SQL passes are reused through exact source/runtime comparison.

Initial proof: [100 executed cases](../proof/2026-10-19-planning-authority/c4-process-atomic-outcome.json),
SHA-256 EBD98D11B3C71C2DDDBB638ED12E2A07B7B388935E2F60D0561A23C5250CC61E.
Its source identity is 63E7C59F239AC6F33E07ECE7428B1B5F9003F36FE823C19D8E3CBDF804E85D83
(9708 inputs); runtime identity is CB9B947A2188B00FE5E4230D10853CB6554FB69592CDF5266095C9EB347D9CD3
(2915 inputs). All 100 cases were covered by the 276.416-second observer; no observed
process or database delta remained, and 14 unattributed temporary paths were preserved.
Full portability passed with all 15651 baseline findings unchanged.

## Repeated SQL fixture correction

The first correction still omitted the required sha256: protocol prefix, so all 18 SQL
cases again stopped at the existing mapper validation (95.848 s). The validator in
ProcessStrategyReceiptValuePolicy was then read directly; the same fixture value now
uses the exact prefixed lowercase form. No production code or assertions changed.
[Second failed proof](../proof/2026-10-19-planning-authority/c4-process-atomic-outcome-final.json),
SHA-256 B4EBA91C7A0384994D29C2E2A1A1A674E310104615D306E12A9E94628C978163,
retains both pre-execution guard corrections and the source/runtime comparison.
The 103.195-second observer covered the complete repeated lane; zero observed processes
and database deltas remained. Nine unattributed temporary paths were preserved.
The fresh full static gate again passed with 15651 unchanged findings. Only this same
18-case lane repeats; the original 82 passing behaviors retain unchanged owning inputs.

## C# Architecture Gate Result

Status: Pass; architecture comparison and corrected SQL evidence agree.

The entry snapshot is `snap-20261020160906-8e205a26`: three scoped projects,
221 documents, 728 types, 6651 members, 208 findings and 189 existing diagnostics.
Three project edges have no scoped cycle. The 1116-line runtime unit of work stays
unchanged; pure transitions, receipt serialization and SQL enforcement remain with their
existing owners. The after snapshot snap-20261020164303-8e205a26 has 729 types and 6655 members;
its 208 findings and 189 existing diagnostics are unchanged. No new findings or scoped
cycles appeared. Direct Runtime (1.650 s), Persistence (2.369 s), Unit (190.512 s) and
Integration (60.924 s) builds passed. The enum and two completion properties account for
the small member delta; the decision store is 325 lines and codec 165 lines.

## Final candidate and validation closure

The final 18 SQL cases pass in 130.283 seconds. The exact comparison retains the initial
55 Unit and 27 existing SQL passes: production sources/binaries and Unit inputs remained
unchanged; only the same Integration fixture and its assembly changed. This is 100
distinct passing behaviors across the correction boundaries, not a claim that all 100
ran on one final binary. Both failed executions remain part of the review evidence.

[Final SQL proof](../proof/2026-10-19-planning-authority/c4-process-atomic-outcome-verified.json),
SHA-256 F4F20828DD2E016EC7CA8016695545E2EE26E5163A734DFD2B2F71CC0E63E669,
retains commands, actual cases, exact comparisons, architecture and resource checks.
The final source identity is D788550A3D551598D6111DD1AF55EBAB91B2B0A6C68582DB56D60D3E4D2A01F3
(9708 inputs); runtime identity is F10F46F2675AAFF0295CA5292F6D1CAC24E3405DBDD18DF1270C9E4D6FBBCA6F
(2915 inputs). Both were unchanged throughout execution. The final Integration build
passed in 62.865 seconds; final discovery matched all 18 expected cases.

Full portability enforcement passed without baseline writes: all 15651 reviewed findings
remain unchanged, with no added or stale findings. Documentation and diff checks pass.
All three retained proof files pass the secret scan, including the private fixture sentinel.

The isolated PostgreSQL 18 fixture on 53619 and existing TestApplication leases were reused.
New tests add no temporary resource family. The bounded observer covered the entire final
execution; no observed process or database-name delta remained. Fourteen unattributed
temporary paths were preserved. Automatic approval review rejected the guarded native
PowerShell deletion before execution with "blocked by policy". These exact owned
directories remain, with CleanupBlocked ownership records; deletion was not retried:
- .artifacts/concurrent-mutations/validation/c4-process-atomic-outcome
- .artifacts/concurrent-mutations/validation/c4-process-atomic-outcome-final
- .artifacts/concurrent-mutations/validation/c4-process-atomic-outcome-verified

Their retained proofs and reconciled processes/database leases remain valid. Cleanup is
not claimed complete. The next outcome-reader preparation directory is separately owned.

Preserve the historical unattributed database, denied probe-output cleanup and unrelated
temporary resources. The earlier full Stable failures are not green. Production producer,
manager authorization/retirement, durable capture/refresh, Workflow, settings/SPA and
sustained capacity remain open. SPA remains on `codex/concurrent-mutations`.
