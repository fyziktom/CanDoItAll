# Held background publication qualification

Status: **Pass, Behavioral, for ordinary publication fencing.** Automatic worker/HTTP
activation, held inherited publication and late cancellation registration remain open.
The two Process race tests use the registered source policy, actual File reservation,
PostgreSQL root transaction and complete recovery dispatcher. They are not proof of a
held full Agent producer immediately before registration.

## Confirmed defect and repair

The regression against unchanged production at `cd6a9f3a0` holds the actual Agent
publication key after original admission. Root cancellation and its durable recovery
queue both complete; releasing the writer still publishes successfully. The failing
assertion and original source/output identity are retained in `confirmed-race`.

Core now carries a transient publication capability from the existing background owner.
The existing File publication scope acquires it after sorted Agent/run keys and before
reacquiring the short workspace gate. The Process module uses its existing dispatch guard
to hold an independent transaction through journal/index publication. It revalidates the
original accepted SQL claim/source and current profile. Source-policy observations stay
before file publication admission and before dispatch: acquiring another source Agent
key from this callback would violate the established order.

The reverse held test observes PostgreSQL cancellation blocked by this actual transaction.
After publication releases it, registered recovery sees and cancels the original journal.
Standalone ordinary background starts use the same reservation owner. Historical
reservation replay does not acquire authority for fresh work. A simultaneous journal and
authority-release failure retains both original errors and the recoverable journal.
The File scope owns release on every path, including failed authority acquisition and
lost file acknowledgement. Inherited schema 7 keeps its enrolled-child owner.

## Validation

All **87 Integration cases** pass, with exact discovery matching the declared selection:

| Class | Cases | Result |
| --- | ---: | --- |
| ProcessAdmissionCancellationRaceTests | 2 | Pass |
| ProcessExecutionAuthorityPersistenceTests | 22 | Pass |
| ProcessInheritedSourcePolicyTests | 15 | Pass |
| AgentToolBackgroundJournalIntegrationTests | 39 | Pass |
| ProcessExecutionStopperTests | 7 | Pass |
| ProcessCancellationCompletionIntegrationTests | 2 | Pass |

Each filter is `FullyQualifiedName~<full namespace>.<class>.`; exact commands and names
are in the proof. Nine affected/dependent production projects and Integration build in
standard Release, using local libraries and serialized outputs. The final candidate's
46 source pins and 29 owning/copied outputs match after execution. Full portability
includes new untracked source and passes without baseline changes: **15674 reviewed
findings unchanged**. Portability/secret tooling tests, rendering checks, documentation
and whitespace checks pass. Broad Stable remains reserved for final integration freeze.

The first test compile lacked an Infrastructure import. Its first native run requested
an interface not separately registered; the corrected fixture casts the registered
workspace store. After the production change, two target-typed test constructors needed
the new explicit guard dependency. These test-only failures remain retained. No production
test failure is omitted, and the known failing race is separate from fixture failures.

## Architecture gate

Status: **Pass with existing follow-ups.** No new production type, partial owner or project
reference is introduced. Core/File remain independent of Process implementation types.
The retained transaction reuses the existing guard and SQL root/row-lock algorithm.

| Owner | Before snapshot | After snapshot | Types | Members before → after | Findings | Diagnostics |
| --- | --- | --- | ---: | --- | ---: | ---: |
| Core | snap-20261022190452-3cb1910f | snap-20261022192031-3cb1910f | 802 | 5931 → 5933 | 228 | 0 |
| File persistence | snap-20261022190501-90bb958d | snap-20261022192042-90bb958d | 148 | 1529 → 1530 | 55 | 10 |
| Process persistence | snap-20261022190507-4d5e01f9 | snap-20261022192050-4d5e01f9 | 200 | 1499 → 1501 | 67 | 10 |
| Process module | snap-20261022190549-12301b65 | snap-20261022192140-12301b65 | 361 | 2919 → 2920 | 143 | 124 |

Finding identities/severities, dependency edges and cycle membership are unchanged.
Existing Core/File type cycles and load diagnostics remain explicit; scoped snapshots
are supported by exact source inspection and successful affected builds. No clean
whole-repository architecture claim is made.

## Resources and proof

The existing isolated PostgreSQL 18 fixture on 53619 is reused. All three observed
executions reconcile with zero new processes, database names or temporary paths. The
final observer covers all six execution commands. Ordinary 5032 and earlier denied
cleanup targets are untouched. Two redundant scan files were removed only after exact
proof/hash checks; verified absence is retained.

[Compact proof](../proof/2026-10-22-held-publication.json): 145174 bytes, SHA256
`FF1B0D0929577ABA65DB9DDCAEA5547ED03958A43F304E27F4D7C858547027E0`.
Local commands, TRX, source identities and failure records remain under
`.artifacts/concurrent-mutations/validation/c4-held-publication`.
The durable proof scan passes with zero findings. Three run-folder scanner matches are
reviewed script-variable assignments in the retained execution helper; no private
password sentinel or actual credential is present. The raw scan and exact review remain.

Proceed to held full-producer registration and inherited publication qualification.
Do not activate the worker from this result. OS restart, bounded owner I/O, live/legacy
fairness, whole-journal capacity, refresh/settings, remaining writers and final E2E gates
remain open. SPA stays on `codex/concurrent-mutations`; SPA main is unchanged.
