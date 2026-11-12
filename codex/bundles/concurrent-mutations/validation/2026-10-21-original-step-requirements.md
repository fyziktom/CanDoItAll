# Original compiled-step requirements

The [design](../plan/P07-original-step-requirements.md) is implemented and qualified.
All 128 original-input prerequisites pass; the final shared-clock candidate passes all
29 affected workspace scenarios. Earlier failures remain retained. Executor activation
stays gated until reviewed completion, manager actions and the actual adapter qualify.

## Implementation and boundaries

Application now exposes the accepted compiled-step requirements beside the original
assignment. Persistence captures plan/runtime indexes once, validates identity and
declared obligations, and writes a bounded version 2 payload. Historical version 1
remains readable but explicitly lacks requirements; execution cannot infer empty ones.
The selected reader still uses one indexed query without plan JSON or neighboring inputs.

The additive migration only widens the existing check constraint. It commits the
`NOT VALID` replacement before validating, allowing ordinary writes during the scan.
Interrupted validation safely resumes. Downgrade refuses retained version 2 rows.
The generated canonical model has no other schema or transfer-mapping change.

Six scoped project references remain unchanged, with no cycles. Three new analyzer
findings concern the generated migration designer and immutable contract member counts.
Both analyzer snapshots retain the same 189 existing NuGet/MSBuild diagnostics; direct
affected Release builds pass. The collector is not wholly green.

## Initial frozen candidate

The [immutable evidence](../proof/2026-10-19-planning-authority/c4-original-step-requirements.json)
is 207,868 bytes, SHA-256
`0D62917952465820493072185864ACA7CFCD3781E4329B9EB7965348741ED37F`.
Discovery and execution both contain 132 cases: 131 pass and one fails.

All 128 prerequisite cases pass: codecs, obligation identity and bounds, indexed
capture, accepted-source immutability, prepared-launch rollback/restart, versioned
migration and selected-input scaling. Three of four actual produced task-plan cases
pass. StepSnapshot with publication allowed receives cancellation at approval
checkpoint entry. The caller supplied no cancellation token; the inherited child has
a 30-second grant and the production coordinator applies that deadline to its lifecycle.
The failure is not evidence of a checkpoint semaphore deadlock.

The repair keeps the production budget and implementation unchanged. This permission
and snapshot scenario will use a local controlled clock after Process preparation,
advance one second at the intercepted provider response, and assert that exact work
charge. A fifth case advances the entire original grant and must fail through the
real cancellation path without publication. This separates functional proof from host
I/O timing while retaining deadline enforcement. This is not a throughput benchmark.

The full portability scan passes final no-write enforcement with 15,663 findings.
Six reviewed additions are generated model names and existing ordinal tool-name
semantics. No stale findings remain. Tool self-tests, rendering register, model drift,
idempotent migration SQL, documentation and whitespace checks pass.

The source manifest contains 9,833 files, SHA-256
`151E07DBF7F557EA22502A347725B5C55FD6EC89AF456D020DF685ECC042950F`.
The runtime manifest contains 2,595 files, SHA-256
`ECFE60B9554E4F9AE33EB20C331480C3C607537755DF5C31DF5442E174BEC761`.
Both match after execution. All 11 commands were observed. There are zero remaining
observed processes, database-name deltas and temporary-path deltas. Candidate ownership
is released; the existing isolated PostgreSQL fixture remains for subsequent work.
Historical cleanup denials remain recorded in the resource ledger.

## Local capture observations

Three warmed capture rounds measure the complete accepted projection:

| Steps | Neighbor prompt characters | Mean milliseconds | Thread allocation bytes | Projection UTF-8 bytes |
| --- | --- | --- | --- | --- |
| 1 | 256 | 0.222 | 50,008 | 4,882 |
| 128 | 256 | 29.648 | 5,740,272 | 557,116 |
| 1,024 | 256 | 229.954 | 46,002,992 | 4,454,764 |
| 128 | 1,048,576 | 291.184 | 10,982,336 | 1,605,436 |

Route-index traversal is constant at two enumerations during index construction for
1/128/1,024 steps. Selected reads retain one query across these shapes. These are local
finite-cost observations, not P95 or sustained concurrency qualification.

## Clock composition characterization and repair decision

The first controlled-time candidate passes 2/5. Its
[immutable evidence](../proof/2026-10-19-planning-authority/c4-original-step-requirements-verified.json)
is 28,676 bytes, SHA-256
`DF9BD196F2068FCB2000C378B20139C6680B6189FCBE2EC0CDD546F5C2E9E8C5`.
The new negative case did not cancel, and the success cases charged wall-clock work
instead of the asserted second. These failures exposed a composition omission:
CanDoItAllAgentWorkspaceFactory constructs a workspace-local AgentToolAdmissionJournal
without passing the host's already registered TimeProvider.

The minimal repair injects that existing TimeProvider into the factory and passes it
to the journal. The factory owns construction; the journal already owns all clock-based
work measurement and cancellation. No new abstraction, dependency reference, timer,
budget override or Core behavior is needed. Default production registration continues
to supply TimeProvider.System. The exact-work and deadline tests must pass unchanged.
Also check ordinary planning entry and inherited workspace lifecycle with the default
clock to detect composition regressions.

The failed characterization candidate preserves all 5,379 production source files and
392 production DLL copies unchanged from the initial candidate. Source and runtime
verification, full no-write portability enforcement and resource reconciliation pass.
Ownership is released. The next candidate will explicitly contain the factory change;
it must not claim that all production bytes are unchanged.

The factory candidate passes 18/21: all 13 default-clock inherited-workspace cases,
three ordinary planning/caller-cancellation cases and two permission refusals pass.
The [retained result](../proof/2026-10-19-planning-authority/c4-original-step-requirements-final.json)
is 94,050 bytes, SHA-256
`D3E6221CE252B4FE0C14451E1C0A0EA807DCF17285CB2BE78D0FFD96B4F2CE51`.
The remaining controlled-clock cases correctly fail original-target validation before
provider work: inherited candidate creation still uses DateTimeOffset.UtcNow, but the
decision now uses the injected clock, so the candidate can appear newer than its decision.

The second minimal repair changes only the timestamp passed by
PrepareInheritedPlanningAsync to CreatePreparingRun. That path has already required
its non-null inherited journal. Use its Clock.GetUtcNow so candidate creation, original
source evidence, decision creation and measured work agree. Preserve existing-only
runs and all original identity checks. Do not alter ordinary background or interactive
creation, rewrite timestamps after evidence capture, or add another runtime partial.

This candidate preserves all production inputs except the factory's two-line wiring
change and its Integration output DLL. Source/runtime verification, full portability
and all three observed commands reconcile with zero process, database or temporary-path
deltas. Its scoped factory snapshot is `snap-20261021095045-4cc89364`; the reported
Hosting/module and ImageGeneration provider/builder cycles are unrelated existing
relationships, and single-project scope does not prove a cross-project graph. The
existing Process graph proof remains unchanged.

The creation-clock candidate passes 6/9: all four selected default-clock inherited
scenarios and both controlled permission refusals pass. The three controlled execution
scenarios reach a second existing clock inconsistency before provider work:
AppendExecutionLogAsync uses wall time, UpdateRunProgressFromLog advances the saved run,
and the bound journal then correctly refuses a backwards timestamp from its registered
clock. The persistence identity guard remains intact.

Its [immutable evidence](../proof/2026-10-19-planning-authority/c4-original-step-requirements-clock.json)
is 92,653 bytes, SHA-256
`2BF3491BCA3790A6C8DB0947C97675A9F87F425F689415012DFC5395BE3E1FB4`.
Only the inherited creation source expression and two Core DLL copies differ from the
factory candidate. Both selected commands are observed; source/runtime identity, full
no-write portability enforcement and zero resource deltas pass. Ownership is released.

The next reviewed repair uses one execution clock throughout the existing workspace
execution owner: the admitted journal's clock when present, and system time for
deliberately journal-free legacy workspaces. Creation, progress, approval, cancellation
and completion must agree. No new partial, interface, project dependency, timestamp
clamping or budget increase is justified. The unchanged five controlled cases and 24
default-clock inherited/ordinary planning cases form the next 29-case candidate.

## Qualified shared-clock repair

The [final evidence](../proof/2026-10-19-planning-authority/c4-workspace-execution-clock.json)
is 100,999 bytes, SHA-256
`2A84894B438BBEEF8C118246E75AB2546C72F109B3C2DF4FD306D72CE8836505`.
All 29 discovered cases pass: five controlled produced-workspace scenarios, thirteen
default-clock inherited-workspace scenarios and eleven ordinary planning-entry cases.
The exact one-second charge, exhausted-grant cancellation, publication permission,
retained original input and existing-only reopening assertions remain intact.

The existing execution owner now uses the admitted journal clock consistently for
creation, progress, approvals, cancellation and completion. Journal-free legacy
workspaces explicitly use system time. The factory supplies its registered clock.
The repair adds one field and replaces 37 direct wall-clock reads; it adds no lock,
query, timer, new partial, project reference or weaker storage check.

Scoped Core snapshots `snap-20261021102450-8e94da1d` and
`snap-20261021103047-8e94da1d` retain the same 784 types, 225 findings, 189 existing
collector diagnostics, three existing cycles and ten member-bearing execution partials.
Only the clock field is added (5,822 to 5,823 scoped members). Single-project scope is
not whole-graph evidence; the unchanged Process graph proof remains the relevant
cross-project check. Direct Core, Agent module and Integration Release builds pass.

Full portability final enforcement passes with 15,663 unchanged findings. Rendering,
documentation and whitespace checks pass. Source identity contains 9,836 files,
SHA-256 `6AD87701FF4972EE38630A3D7123ADFFB02A2BB43B2D4FA18E9B4F63BA4EF8BD`;
runtime identity contains 2,595 files,
SHA-256 `3700EAFA1B8F2E25DFABD1DCF5EF4D2C3AA782465675FCC175A2D437B5A3FE3F`.
Both are unchanged throughout execution. All three commands are covered by the
1,007-second observer; no observed process, database-name or temporary-path delta remains.
Ownership is Released. Historical cleanup denials and sustained-capacity limits remain.
