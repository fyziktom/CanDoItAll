# Agent cancellation signal lifetime

Entry: behavioral Pass after P13 qualified 367 distinct cases. Its signed checkpoint
has been attempted and is waiting for GPG. Implementation proceeds on disjoint files;
no changes are added to that staged checkpoint. The durable root worker cannot depend
on synchronous user callbacks. The
selected stopper currently calls RequestCancellation before its separate 250 ms lease
acquisition bound, so that lease bound says nothing about callback duration.

## Ownership and behavior

Keep the existing registry boundary and exact execution identity. RequestCancellation
continues to return false for absent/disposed registrations and true for an accepted
signal, including repeats on a live registration. By-process cancellation counts only
new signals. Both signal cancellation immediately but no longer wait for callback
completion. The token must already report cancellation on return. Do not claim the
callbacks finish within a deadline, or that notification alone settles measured work.

Replace linked-source synchronous propagation with a registered upstream signal that
uses the same lifecycle. A pre-cancelled upstream token must produce a cancelled child
token before Register returns. Explicit and upstream signals share one CancelAsync
invocation and one observed completion task per registration. No Task.Run per request,
unbounded retry queue, discarded fault, semaphore held while awaiting callbacks, or
provider dependency is needed.

Use a small internal registration owner in Agent Core. It owns the source, upstream
registration, lock and notification task; it implements the existing public registration
contract, extended with IAsyncDisposable for an explicit optional drain. The registry
retains exact-key/exact-object removal and owns the dictionary only. Remove the previous
record/wrapper duplication. Register rejects Guid.Empty and duplicate execution IDs.

Dispose marks the registration closed and unhooks upstream cancellation without waiting
for running callbacks. Requests then return false. If notification is in progress, retain
the dictionary identity and source until it finishes; re-registration of the same
execution is refused during that interval. A late disposal cannot remove a replacement.
DisposeAsync requests the same closure and awaits the retained release task. Both are
idempotent. Never dispose the source while its callbacks execute. Capture the token at
construction so observing cancellation does not race source disposal.

The upstream registration must use nonblocking Unregister rather than Dispose while
holding the registration lock. Its callback only invokes the same short signal method;
consumer callbacks run through CancelAsync. Construct/publish/attach ordering must be
reviewed for pre-cancelled tokens, duplicate registration and concurrent explicit signal.

Prefer retaining the observed notification Task itself over allocating a second
TaskCompletionSource for every registration. Initialize it to Task.CompletedTask, mark
notification running before calling CancelAsync, and assign the observed completion
while holding the registration's short lock. Its finally path clears the running bit
and releases the source if disposal was requested. DisposeAsync marks disposal then
returns that same task. This removes an extra per-registration allocation and prevents
a disposal caller from observing an unassigned notification task. No await occurs under
the lock. Create the upstream registration only after publishing the exact entry; no
consumer can yet access the token. A duplicate rejected entry has no upstream callback
and its exact-object removal cannot remove the existing registration.

This ownership covers the notification started by CancelAsync. As with ordinary .NET
tokens, registering a callback after cancellation may invoke it synchronously on that
registrant's thread; it is outside that earlier notification task. Consumers must finish
using their registration before disposal. A callback may call nonblocking Dispose, but
must not synchronously wait on its own notification via DisposeAsync. The optional
async drain is for the execution owner outside its callback.

Observe callback faults once. Log the execution ID, failure count and exception types
without arbitrary callback exception messages, stack values or request data. A failure
does not undo the signal, invent a terminal journal, or prevent final source release.
Require a logger at production construction; use the existing execution-service logger
for its compatibility construction path, and explicit test loggers in fixtures. Do not
silently substitute NullLogger in production.

## Compatibility and boundaries

All source callers were inspected: exact selected stop and the general Process observer
consume only the signal result/token state, not synchronous callback completion. No
other registration implementation was found. The general Simple Chats registry is a
different owner and is unchanged. Core owns callback lifetime; Process Persistence and
the selected stopper do not acquire new dependencies. Existing registration IDisposable
callers remain valid; callers that need complete release may await DisposeAsync.

The public addition and constructor change need direct Core, Module and Web build proof.
This is not a new root recovery worker or automatic activation. If any external package
implements the registration interface, its compatibility must be assessed before release.

## Validation contract

Use deterministic callback-entry/release barriers with finite failure deadlines, not
timing sleeps. Prove explicit and upstream signals return while a callback is held;
token state changes immediately; repeated/concurrent exact/by-process/upstream requests
execute each callback once; siblings are unaffected; duplicate registration and empty
IDs fail; disposal before signal refuses it; disposal during callbacks is nonblocking;
async disposal waits then releases; callback-initiated disposal cannot deadlock;
registration after full release works and old repeated disposal cannot remove it.
Throwing callbacks must be observed/logged once, must not expose their secret-shaped
message, and must release resources after disposal. Test already-cancelled upstream and
an upstream/explicit/disposal race. Repeat the existing observer, selected-stop and
inherited Agent integration cases with exact rebuilt discovery counts.

Read the current Microsoft CancelAsync contract before implementation. Refresh scoped
Core architecture facts and the final portability scan. Record callback lifetime and
retained-registration limits honestly; this prevents stop-call blocking but does not
make arbitrary consumer cleanup preemptible. Preserve the ordinary instance and SPA
branch. No merge, push or external message is authorized by this design.

Current scoped Core baseline: snap-20261022044934-ee223b1b, 795 types, 5887 members,
1060 dependency edges, 226 findings, three pre-existing type cycles, no diagnostics.
No architecture-clean claim is made about those existing cycles. Microsoft contracts
verified on 2026-10-22: CancelAsync synchronously marks the token and returns callback
completion; Unregister does not wait for an already executing upstream callback.
Sources: https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtokensource.cancelasync?view=net-10.0
and https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtokenregistration.unregister?view=net-10.0.

The internal registration receives a narrow exact-removal callback from the registry,
not a reference to the registry type. This is lifecycle composition (as in the existing
Simple Chats registration), not a service-locator or a new interface. The callback
captures only registry ownership; never capture the entire ExecutionRunRecord, which
could retain large prompts/journals. The registration stores just its GUID, normalized
Process run ID, source/token, upstream handle and logger. Dependency review must retain
the same three pre-existing Core type cycles and no new cycle.

## Exact edit and regression surface

Production: Core/Execution/AgentExecutionCancellation.cs, new internal
Core/Execution/AgentExecutionCancellationRegistration.cs, and the single compatibility
construction line in Core/Execution/AgentFrameworkWorkspaceExecutionService.cs. Existing
Hosting and Module registrations should resolve the public typed-logger constructor
without registration changes. The non-generic logger overload is internal for the
existing workspace execution owner. No Process or Workbench source edit is planned.

Tests: extend AgentExecutionCancellationRegistryTests with isolated lifecycle cases;
update explicit logger construction in AgentFrameworkProcessRuntimeCancellationObserverTests,
AgentInheritedParentStopFixture (its registry uses target-typed new), and Integration
AgentInheritedParentStopTests. Add a real file-backed selected stop with a held cancellation
callback, proving Signalled returns without mutating the journal and that its exact
registration drains afterward. Repeat these owning classes and the ProjectTaskPlan
Process child-stopper adapter, then add a targeted DI resolution test for both existing
registration paths if current smoke coverage does not resolve the registry.

Before execution derive exact current method/case counts, refresh direct Core, relevant
Module/Hosting/Web and Unit/Integration outputs in standard Release, and record a finite
resource plan. Build/test sharing outputs remains serialized. Callback tests must release
their barriers in finally even when an assertion fails, then await registration disposal
before disposing callback handles; otherwise a failure could strand the test runner.

## Qualification, 2026-10-22

Behavioral gate: Pass for this signal-lifetime boundary. Exact rebuilt discovery and
execution both contain 60 cases; all pass, including 13 new lifecycle/composition/real
file-backed stop cases. Six standard Release builds pass: Core, Hosting, Agent Module,
Web, Unit and Integration. The source and Git revision stayed fixed during compilation;
all source/runtime bytes stayed fixed throughout discovery and execution. Five test
commands are covered by the resource observer and leave zero new process, database or
temporary-path residue. Full portability enforcement passes with 15665 reviewed findings
unchanged; no baseline was rewritten.

C# architecture gate: Pass with existing findings recorded. Scoped Core changes from
795 to 794 types, 5887 to 5901 members and 1060 to 1058 dependency edges. The same three
pre-existing type cycles remain. Two new informational size findings cover the cohesive
93-line notification owner and 71-line registry; no new cycle, project reference,
partial, service locator or Process/Workbench dependency was introduced. Both actual
DI registrations resolve the same logged singleton without creating a workspace.

See [validation](../validation/2026-10-22-cancellation-signal-lifetime.md) for exact lanes,
input identities and retained proof. External binary compatibility of the public
constructor/interface change remains a release integration item. A blocked callback
retains its registration; this does not prove callback preemption or global storm
capacity. Durable recovery and all later integration remain open.
