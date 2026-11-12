# Read cost and receipt checkpoint: implementation entry draft

Priority changed after selected parent-stop validation. Its 58 new cases pass, but an
existing interrupted-receipt recovery case exceeds the unchanged two-second checkpoint
deadline. It failed once in the fourteen-case outcome-reader lane and in isolated cold
repeats on identical source/binaries. The final read-authority check was cancelled in a
catalog file read in the first run and while opening an unpooled PostgreSQL connection
in the first repeat. Preserve these failures; they are not successful recovery proof.

## Measure before changing behavior

P10 already measures complete HTTP review reads with 27 Process SQL commands. Its fixed
one-second finite budget remains open. SQL execution time excludes connection opening.
ReadOriginalDecisionAsync creates one short-lived DbContext, then reads runtime/claim,
accepted source, selected child and exact material through multiple statements. With
the test fixture's deliberately disabled pooling, each automatic open/close can become
a new physical connection. This is a hypothesis to measure, not an established sole cause.

Use the existing bounded ActivityListener in ProcessPlanningReviewReadScalingTests.
Add separate spans for AgentInheritedPlanningRunReader detail retrieval and current
authority validation. Instrument original Process read authority, catalog lease and
project admission stages only if those spans leave a material unexplained interval.
Extend the existing test DbCommandInterceptor with IDbConnectionInterceptor's
ConnectionOpenedAsync and ConnectionOpened observations: count opens and sum each
ConnectionEndEventData.Duration, scoped to the same selected HTTP request. Keep SQL
execution counts/times separate. Inclusive nested activity times cannot be added.
Do not log SQL text, IDs, payloads, connection strings or credentials. Bound observations.

Official EF Core contract inspected:
https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.idbconnectioninterceptor.connectionopenedasync?view=efcore-10.0

Run the existing three diagnostic shapes (RunSnapshot/1, StepSnapshot/1 and /1024), with
two warmups and five observed reads each, on the same reusable isolated server. Retain
host-load windows and actual allocations, statements and connection counts. Do not
change payload shape, pooling, reader authority, discovery, clock or acceptance budgets.

## Candidate optimization, only if measurements support it

Keep one explicitly opened connection for each short-lived Process authority-query
context until that context is disposed. It contains only sequential SQL reads; it does
not span Agent file/catalog waits, providers, user interaction or a new transaction.
Every current authority read still creates a fresh context and repeats all original
source, grant, material, profile and runtime checks. No cached authorization, snapshots,
new dependency, global connection, retry fallback or increased deadline is introduced.
Preserve existing nonrelational test behavior explicitly if affected consumers require it.
Verify disposal after success, refusal and cancellation, and retain exact exception paths.

The production owner remains Process.Persistence. Core owns only its reader spans;
diagnostic interception remains in the test project. No new project references, partial
classes, persistence entities or service-locator behavior. Inspect before/after scoped
CodeAnalytics and actual project files. A connection-lifetime helper is justified only
if it avoids repeated ownership/disposal logic; do not add an abstraction for one await.

## Qualification

Compare before/after connection counts with unchanged SQL statements and result shape.
Add actual connection-lifetime tests, including cancellation/error disposal and source
revocation between queries. Run the original source/dispatch/read-authority tests and
the failed recovery case, then complete the selected-stop regression lanes. Repeat the
unchanged six-shape finite HTTP gate after diagnostic evidence supports it. Subsequent
sustained-capacity and final integrated gates remain required; one warm success is not
capacity proof. Snapshot/source-policy settings and SPA integration remain gated.

Keep all earlier failed runs and explicit unused lanes in their retained checkpoint.
Do not merge SPA or change its colleague-owned main branch. Root quiescence design is
prepared separately and resumes after this performance-dependent regression is resolved.

## Entry decision (2026-10-22)

Governed repair of the failed P11 prerequisite; downstream cancellation recovery remains
blocked. The first stage changes telemetry only: two Core read spans and test-only
connection counting. It retains every authorization check and all existing deadlines.
Reuse P11 Core snapshot snap-20261022012301-ee223b1b and Persistence snapshot
snap-20261022012340-531e33b7 as the exact pre-change architecture evidence. The existing
ProcessReadProbe owns observations; no new runtime abstraction or project reference.
Run the three existing diagnostic cases with confirmed discovery (3). Build Core and
its downstream production graph and refresh Integration Release outputs first. Then
freeze source/runtime identities, run observations on the existing isolated server and
verify resources. Only measured evidence permits the connection-lifetime optimization.
The six finite-budget cases remain a separate acceptance gate, not diagnostic evidence.
No broad-suite run is justified by this diagnostic stage alone.

## Measured decision

The unchanged baseline passed all three diagnostic cases (15 samples). Every request
executed 27 Process SQL commands and opened 22 connections. Median connection opening
cost was 165.19 / 161.00 / 156.63 ms for RunSnapshot/1, StepSnapshot/1 and StepSnapshot/1024;
SQL execution was 131.76 / 138.64 / 145.33 ms. Median complete reads were
778.21 / 794.96 / 836.25 ms, with one RunSnapshot sample at 1058.90 ms. This is useful
diagnostic evidence, not a passed finite-budget or sustained-capacity gate. Source and
runtime identities remained unchanged; the run left no process/database/temp delta.

Narrow the optimization to ReadOriginalDecisionAsync, which owns the repeated original
authority reads. Leave dispatch, reservation and caller-owned mutation transactions
unchanged. Explicitly open one relational connection around its four SQL reads and their
bounded in-memory source validation; close it in finally, including cancellation, early
refusal and error. Preserve the supported test-only InMemory path. No new transaction,
authorization cache, interface, dependency or partial class is needed. This holds a pool
slot between those queries; it never spans file, provider or user waits. Capacity remains
an explicit downstream gate. EF's [connection lifetime guidance](https://learn.microsoft.com/en-us/ef/core/performance/advanced-performance-topics)
supports the explicit close before context disposal/reuse.

Add nine focused connection-lifetime cases: both snapshot modes, missing claim, changed
material, cancellation before/during a query, injected read failure, material changed
by a second context between queries, and the nonrelational path. Assert query counts,
fresh connections on repeated calls, no transaction/writes and closed connections after
every exit. Run the failed receipt case first, then these cases, original-source (30),
inherited-source (15), outcome-reader (14), selected-stop adapter (10), the remaining
P11 regression lanes (98), the three diagnostic shapes and six finite-budget shapes.
Confirm each discovery count before execution. Review scoped Core/Persistence architecture
and full portability enforcement on the final source. Do not widen to Stable until the
integrated activation checkpoint or another named invalidation requires it.

## Selected dispatch read extension

The first optimization passed all nine connection tests and the previously failing
receipt-recovery case. The three diagnostic shapes passed, with 16 rather than 22 opens
and unchanged 27 SQL statements. Five of six finite shapes passed; StepSnapshot/1024
reached 1004.32 ms, so the unchanged one-second gate still fails. The run retains 18 passed
and one failed execution, with 167 unexecuted regressions and zero resource delta.

The source-policy read path also calls ReadDecisionDispatchAsync for inherited children;
it still opens separately for its four queries. Apply the same connection lifetime to
that selected path. A private WithSelectedReadContextAsync delegate wrapper owns the
fresh context/open/finally-close for these two callers. It avoids duplicate resource
handling and keeps all SQL validation in its existing owner. Generic dispatch,
reservation, mutation transactions and public contracts stay unchanged. The connection
still spans only sequential database reads and bounded source validation.

Exercise every connection-lifetime case against both selected paths (18 cases), with
current-dispatch versus historical-read outcomes asserted explicitly. Include the 40
existing selected-dispatch authority cases in regression selection. Total discovery is
235 executions / 234 distinct cases, including the repeated receipt case. Retain the
first failed finite gate and compare the same diagnostic shapes again; no deadline,
pooling, source authority or acceptance criterion changes.


## Qualified checkpoint

[Final qualification](../validation/2026-10-22-selected-read-connections.md) passes 235 executions / 234 distinct cases,
including all 18 new lifecycle cases and six finite shapes. The final request uses ten
connections for the same 27 SQL statements; earlier failed evidence is retained.
Continue with the root lifecycle gates. Sustained capacity and activation remain open.
