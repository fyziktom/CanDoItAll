# Send-state repair and receipt baseline candidate

Status: six Release builds and final static gates passed. All 80 focused cases passed;
all 22 performance rows failed during probe initialization before any workload sample.
The preceding [baseline preparation checkpoint](P02b-R1bc-baseline-candidate.md)
remains 78 passed / two failed / zero skipped out of 80 focused cases. Its 22
performance rows were discovered but never executed.

The Send action now receives the native host's existing `BlocksNewMessage` decision
through an optional `IsSendBlocked` value in the wrapper and presentation. The shared
composer combines it with existing busy/approval conditions. Draft editing, attachment
editing, busy state and the host dispatch guard remain unchanged. Four production
files changed; the existing native failure is retained as the regression test.
The three affected rendering-register entries were reviewed and merged individually.
Historical viewer source and its register entries are excluded.

The reviewed transfer has SHA256
`6E9F8B6EF07A2D9D5B6C28A2D4B8AA5ED50C4E1C0B606B059A58EEB321800E12`.
Two `Description` attributes on API execution-state properties now list all seven
numeric values, preserving the continuation explanation. These edits change no
serialization or runtime state transitions.

Expected focused proof remains 57 native Components plus 23 API documentation cases.
The receipt harness still selects 22 LongRunning rows, each executed in a fresh host
after exact aggregate/one-row discovery. The prior source-only runner review still
applies to the same deadline/output-pump/cleanup mechanics; only candidate evidence
paths and refreshed source identities change. No production JSON optimization is
included. Owned file fixtures are created only inside pre-recorded per-row roots.

Evidence root:
`.artifacts/concurrent-mutations/validation/r1bc-send-state-baseline-validation`.
The native host/component tests prove state ownership and draft retention. A real
browser check of the disabled action with an editable draft remains required before
UI closure; a passing renderer or receipt-cost measurement cannot replace it.
All earlier broad-suite, performance corroboration, signing and cleanup limitations
remain in force. No end-to-end or capacity closure is claimed.

## Executed checkpoint

The frozen candidate executed 102 cases: 80 passed, 22 failed, zero skipped. Exact
discovery, labels, definitions, execution IDs and final source/binary identities
matched. Complete source inventory: 8,603 inputs, SHA256
`9FC944F974783D3E058BD80A2E0219FEF27103804BA6FB2CCAD6129992B1CF30`.
Summary SHA256: `63CB945C44DC5C188B07D409CD145BC3C878AA7CEF6134756864D3048BC72AFC`.
Portability enforcement passed with 15,467 unchanged reviewed findings; all 17 static
self-tests, 1,198 rendering inputs, 426 maintained documents and whitespace passed.

The performance listener queried `AgentFrameworkTelemetry.ActivitySource.Name` while
the same static source was being initialized. Every fresh host therefore threw the
same type-initialization failure. No workload or measured sample ran. All 22 owned
fixture roots were removed and all 22 test hosts exited, independently verified.
This is a harness failure, not a performance baseline. The next candidate uses the
existing compile-time `SourceName` constant; no production telemetry or store changes.
See [the probe repair candidate](P02b-R1bc-probe-baseline-candidate.md).
