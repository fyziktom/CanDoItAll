# Active-work publication version and transaction boundaries

The repaired candidate passed **47/47**: 14 operation-contract cases and 33 real
PostgreSQL publication/currentness cases, including all seven new active-work cases.
V2 requires the typed held-authority work guard after authority acquisition, after the
project wait and immediately before SQL commit. V1 retains its original wall-time
meaning and digest domain. Both retain the original receipt identity and reject mixed
versions, stale material, lost authority and expired basis. Replay uses the original
receipt without spending mutation work again.

The initial 47-case candidate passed 46. Its failure was the existing currentness
test's deletion scenario calling the retired Guid-only API (not cleanup and not an
active-work failure). The repair uses `ProjectLifecycleTestDriver`, which captures and
dispatches the real original lifecycle submission. All prior frozen production inputs
are unchanged. The initial reconciliation outcome text incorrectly called it an
active-work test failure; its retained TRX identifies the actual failing test.

Standard Release builds, build-backed discovery, rendering, documentation, diff checks
and full portability enforcement passed for both candidates (15,610 unchanged reviewed
findings). The repair runs the complete 47-case lane again.

CodeAnalytics `snap-20261019054256-bf63c30c` covers four projects, 569 documents and
1,761 types. There are no project cycles or changed references. Existing Workbench
module and local type cycles do not involve the six changed planning files. Its 214
diagnostics include 189 existing package advisories (reported as errors), seven
truncated diagrams, two duplicate generated-type names and sixteen factory-inference
notices/warnings. The analyzer therefore reports blocking errors; this scoped review
does not claim repository-wide package or architecture health. No new composition
registration, store, lock, project or partial was introduced.

| Candidate | Source SHA-256 | Runtime SHA-256 |
| --- | --- | --- |
| Initial | `8B54C5B414AD7CA6461C6E192ED453551E9209FD313E455D7AA212D643D41F78` | `BF48AFFDF4DD6141BDCE77C36C5B3DDFC96850EC607A1495DEC617E824F82B99` |
| Repair | `86000E8C4C222E52C193362D4568536F8E3DC55DBDDC42F376211B877C386D9E` | `1C49D754B846ACC29D02B434DD469017ADF976EEC0C8B031FB61DBA2C2EAB691` |

Final comparisons confirm 9,200/9,201 unchanged source inputs and 6,436 unchanged
runtime files. Both observers exited with no remaining observed processes or database
name delta. Six/seven TEMP deltas without ownership evidence were preserved. The
shared PostgreSQL fixture is retained for the following integration. The separately
reported profiler cleanup blocker remains unchanged.

Retained proof under `../proof/2026-10-19-planning-authority/`:

- `c4-active-work-publication-validation.json`: `4B0FF49557061F7FBC61C440A6FC43E26CF230FF8F43443D239865F15ED91490`.
- `c4-active-work-publication-repair.json`: `7B8D9EAD587B80040FC239810A9CC10D81842A62F45B14F0793C9C669326A67F`.

Freezes are released after the final identity/resource comparisons. This qualifies
the publisher perimeter, not the actual Agent capability adapter. Saved actions,
Workbench participant, product admission and sustained/max-size qualification remain
open until their own integration proofs pass.
