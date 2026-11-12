# Bounded cancellation observation

All 32 selected cases pass: eight existing codec cases, 13 native PostgreSQL observation
cases, three real HTTP cases and eight queue regressions. Eight affected standard Release
builds pass. Final identity checks preserve 456 source pins and all 50 owning/copied
outputs. Both native attempts reconcile with no new processes, database names or temporary
fixture paths. Full portability, documentation, whitespace and exact delivery artifact scans pass.

## Behavior and ownership

`IProcessCancellationProgressReader` is an Application read boundary. Persistence owns
one selected, five-second-bounded PostgreSQL query. The existing Processes module registers
it; Web maps only the public fields through a dedicated endpoint owner. No project,
migration, partial, queue claimant or continuation owner was added.

`GET /api/processes/runs/{runId}/cancellation` requires the current Process read permission
and returns `Cache-Control: no-store`. Runtime status is separate from cleanup tracking.
Child identity, no request, untracked historical stop, pending work, verified completion
and reconciliation are explicit. A cancelled runtime row remains pending until its
completed queue checkpoint is consistent. Reads never seed, claim, advance or repair.

The query bounds position bytes before materialization and reuses the canonical codec.
It reads intent/lease presence as booleans and never materializes their private payloads.
The response exposes original barrier identity, bounded stage/pending metadata, timestamps
and decimal-string progress revision, without action payloads, leases or cursors. A child
does not observe its ancestor's queue. Missing and empty IDs return 404 and 400 respectively.

## Native evidence

The database cases cover selected root/child identities, no request and historical state,
the original pending barrier, real engine/queue completion, eight invalid checkpoint
shapes, unchanged original records/receipts, independent database-profile isolation,
caller cancellation during a real table-lock wait and indexed lookup with 2,000 historical
roots. No cleanup completion is inferred from runtime status alone.

The history case executes one selected statement. Both `PK_process_runtime_states` and
`PK_process_cancellation_recovery` are used; each index returns one row with one loop and
there is no sequential scan. Adapter elapsed time is 11.2252 ms; the separate actual plan
execution reports 0.063 ms. This is one observational sample, not a sustained latency SLA.

HTTP cases exercise the real registered adapter and authentication owner. Anonymous,
execute-only and write-only callers are refused; Process read access succeeds. Permissions
are evaluated again after the token changes. Exact retained rows remain unchanged and
private lease/action identifiers never appear in the response. Public route discovery
contains the endpoint. The legacy stopped-root response remains explicitly untracked
while automatic recovery is disabled.

## Retained failures and review

The initial test build found one raw-string terminator error, corrected before discovery.
The initial observer rejected a missing ownership-schema discriminator before any native
execution; its original precreation record and correction are retained. The first native
selection executed 21 cases: 19 passed and two fixture errors failed; later lanes did not
run. One attempted child launch lacked the required parent claim; the other constructed
an invalid strong ID before reaching the reader. The corrected tests establish the real
parent claim, require successful fixture commits and pass the default ID to the reader.
Production source is unchanged between native attempts. Their exact failures, original
test source, source/output identities and zero-residue reconciliations remain preserved.

Scoped before/after snapshots cover Application, Persistence, Processes module and Web
API. No new dependency cycle or project reference appears. The only added finding is
informational: the fixed SQL projection record exposes 16 source members. It contains
bounded data, no behavior or service ownership. Design-time load diagnostics remain
10/0/123/180 respectively for Persistence/Application/module/Web; direct builds and real
host tests provide authoritative compilation and registration evidence.

## Remaining integration

This is the observation part of [P17](../plan/P17-cancellation-observation-and-admission.md).
Finite root mutation admission, legacy/native cutover and automatic worker activation
still require their own proof. Existing `/cancel` continues its synchronous behavior.
Sustained and separate-process capacity, Process refresh, Workflow/settings, remaining
mutable owners and final Stable/E2E remain open. SPA stays on its separate feature branch.
Earlier denied cleanup resources remain recorded and were not retried.

## Static qualification

The complete scan includes untracked source: 10,016 files and 46,585 raw findings. One
added protected finding is reviewed as an HTTP route string matching the scanner's
`/proc` heuristic. The inspected baseline delta contains only that fingerprint/count and
timestamp. Final enforcement without `--write-baseline` passes with 15,677 reviewed
executable-source findings. The 6/4/7 portability/secret/rendering tool tests and rendering
register check also pass. Initial failed enforcement remains retained.

The [compact proof](../proof/2026-10-22-cancellation-observation.json) preserves actual
results, source/output identities, exact command/log hashes, both resource observations,
the initial failures, architecture and reviewed static evidence.

Both local artifact scans report three reviewed PowerShell-source heuristic matches and
zero credential-sentinel matches. Neither skips oversized/unreadable text. The exact
compact delivery proof scans with zero findings. The new verified run's two bulk scan
outputs were separately removed after containment/hash checks; their summary and hashes
remain in proof. Earlier denied cleanup targets were not retried.
