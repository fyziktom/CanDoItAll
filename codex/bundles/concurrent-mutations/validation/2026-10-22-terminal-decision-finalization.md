# Terminal decision finalization — 2026-10-22

Behavioral gate: **Pass** for the owned P15 outcome. All **312** final selected cases
pass, including **94** new cases. No earlier pass is reused after production repair.
The full concurrent-mutations bundle remains open.

## Behavior and architecture

An explicit Process command closes an empty or fully accounted generation while retaining
the persisted terminal step and run status. It requires the exact generation/budget,
complete child closure and settled allocations, without a live claim. A small Application
coordinator binds the explicit action and actor, recovers the original receipt first,
and commits through the existing runtime owner. Persistence performs one exact guard
projection plus a bounded 65-row child read without hydrating material payloads.
No child, work, outbox event or business effect is fabricated. Version 6 uses explicit
primitive wire identifiers with typed reconstruction; previous receipt versions retain
their representation. Shared scopes close through their existing independent owner.

The P13 root guard also now permits a matching new cancelled strategy result to settle
its original child and cancel a quiescent root in one transaction. It retains early
admission checks, then requires persisted result acceptance and independent full-root
quiescence after the flush. Any remaining work throws before commit and rolls back all
result, accounting and receipt changes. Ordinary cancellation retains early refusal.

Scoped architecture gate: Pass with existing diagnostics recorded. Persistence changed
from 182 types / 1389 members / 209 edges to 184 / 1400 / 210, with 32 entities, 62 findings
and no cycles. Snapshot: `snap-20261022065944-4d5e01f9`. Ten existing WS0002 package-advisory
diagnostics remain; direct compilation succeeds. Runtime: 254 types / 1641 members /
409 edges / no cycles (`snap-20261022060845-b3c18571`). Application: 376 / 4067 / 471 /
no cycles (`snap-20261022060851-d27bf2c1`). No new references, schema, interface, partial
or service locator. The existing unit of work retains 43 members; the dedicated root
guard carries its narrow transaction-ordering responsibility.

## Discovery and final execution

| Exact owning lane | Passed |
| --- | ---: |
| Integration-ProcessDecisionCompletionPersistenceTests | 38 |
| Unit-ProcessDecisionFinalizationTests | 38 |
| Unit-ProcessDecisionFinalizationCoordinatorTests | 20 |
| Integration-ProcessDecisionFinalizationIntegrationTests | 23 |
| Unit-ProcessDecisionGenerationTests | 29 |
| Unit-ProcessDecisionCompletionTests | 33 |
| Unit-ProcessDecisionTerminalAccountingTests | 57 |
| Integration-ProcessDecisionPersistenceTests | 31 |
| Integration-ProcessRootQuiescenceTests | 19 |
| Integration-ProcessRootWorkAdmissionTests | 14 |
| Integration-ProcessDecisionTerminalAccountingIntegrationTests | 10 |

All lanes used `dotnet test <owning project> --configuration Release --no-build
--no-restore -p:UseLocalCanDoItAllLibraries=true /m:1 --filter <exact class prefix>`,
with TRX and normal console logs. Build-backed read-only discovery matched all 312
cases before execution. The 38-case completion lane ran first. Five affected Release
builds passed: Persistence, Process Module, Web, Unit and Integration, with
`--no-restore -p:UseLocalCanDoItAllLibraries=true /m:1`; test projects additionally used
`--no-dependencies`. Unchanged Runtime/Application outputs remain in the runtime manifest.
No custom output directory or configuration was created.
After correcting the test clock and expected early refusal, Integration was rebuilt
once more. An exact bridge proves all production sources and binaries remain identical
to the five-build candidate; all 312 final tests use that qualified production output.

Full portability enforcement passes: **15665 unchanged findings**, zero baseline rewrite.
Rendering register, documentation and `git diff --check` pass. All source and runtime
inputs remain fixed during final compilation/discovery/execution; the final build's Git
revision was unchanged.

- Final source manifest: `70188008653E83818DEF2BCCC04E7325A8C3A107C09052C1F29963FD34951F5E`.
- Final runtime manifest: `F6C352894FDE0E5EB342CE5F14CBBDDA639CA11850CBB81C53BE93427F07DD94`.
- Compact UTF-8/LF proof: `codex/bundles/concurrent-mutations/proof/2026-10-19-planning-authority/c4-terminal-finalization.json`.
- Proof bytes: **765904**; SHA256 **37E60C654F3CE569D0E6DE40AC5EE58D9EE1BEAF49B3BDB5EF8D25AE056A66E7**.

The proof passed the artifact secret rules and private fixture-password sentinel scan.
It retains exact commands, source/runtime manifest identities, discoveries, every test
result, architecture review and resource coverage. This is affected-scope validation;
the final integrated Stable/capacity/browser gates remain downstream.

## Retained failures and repairs

| Candidate | Executed | Passed | Failed | Unexecuted |
| --- | ---: | ---: | ---: | ---: |
| Initial codec | 38 | 34 | 4 | 261 |
| Explicit wire IDs | 81 | 79 | 2 | 218 |
| Two test-method corrections | 177 | 175 | 2 | 43 |
| Post-flush root guard | 38 | 31 | 7 | 274 |
| Corrected clock/refusal fixture | 312 | 312 | 0 | 0 |

The first four failures exposed read-only ID wrapper deserialization in version 6.
Explicit Guid/revision wire fields corrected production code. The next two failures
were test-only: a scope revision reused before its membership join and an ambiguous SQL
probe. Their exact two-method/source and Integration DLL/PDB bridge is retained as
historical evidence. The next two failures were unchanged cancelled-result assertions:
P13 checked outstanding work before the same transaction settled it. The production
ordering fix required the complete final rerun. The next 38-case lane passed all original
cases, while six new descendant fixtures used an expired clock and one forged original
was correctly refused earlier by optimistic concurrency. Only that test file changed;
its whole class was repeated along with the 274 unexecuted regressions. There are 646
executions across all attempts, including 15 historical failures; only the final 312 passes qualify the
current code. No failed or unexecuted lane is described as passing.

## Resources and continuation

All 22 test commands across the five attempts are covered by the resource observer.
Every attempt ends with zero new process, database-name or temporary-path delta. The
existing isolated PostgreSQL 18 server and standard outputs are reused and still needed
for durable recovery. Ordinary 5032, its databases, installed MCP tools, unrelated
resources and the separate SPA branch remain preserved. Earlier automatic cleanup
denials remain unresolved and were not retried.

Next: bounded durable root recovery with database-clock leases, retained original intent,
fair finite stages/backfill and operational resolution. Unknown Agent work/effects remain
pending. Snapshot refresh, producer/Workflow/settings integration, remaining writers,
historical-volume/sustained capacity and complete E2E closure remain open.
