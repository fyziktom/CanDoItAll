# Selected Process child dispatch authority

Status: designed before implementation; the query now has
[86 passing selected cases](../validation/2026-10-19-selected-child-dispatch.md).

A selected planner needs the existing exact claim/source authority plus its original
decision child, material scope/stamp, admitted policy and registered content/execution
contracts. Do not broaden the legacy background reservation or mutation guard.

Add a distinct ReadDecisionDispatchAsync entry on the persistence authority query.
Keep the ordinary ReadDispatchAsync fingerprint bytes and non-null-policy refusal.
The selected entry reuses the exact run/step/claim/Agent/creation-interval and original
prepared-launch checks, then uses the existing bounded child query and a material
scope/stamp projection that excludes PayloadJson. It must compare the admitted policy
canonically with the prepared launch, validate original scope ownership, exact child
execution ID, executor kind, grant and supported contract pair. The original policy
and canonical parent binding enter a separate versioned fingerprint domain.

Historical matching children remain readable after closure, refresh, cancellation,
claim expiry or settlement. New dispatch additionally requires an active root/run,
claimed/running step, the exact evidence token/attempt with an unexpired lease,
current Active generation and shared run scope, no closure/settlement/overrun, and
positive work. Read checks do not acquire a root lock; effect publication still uses
the enlisted root fence. No read returns permission to skip that fence.

Observe the clock after all awaited metadata reads. A lease expiring while the final
read waits must be refused, and the returned observation timestamp must describe that
final check. Prove this with an interceptor that advances the injected clock during
the last SELECT, without a wall-clock sleep.

Keep selected validation in a cohesive persistence helper rather than adding another
runtime partial. No Core-to-Process, Workbench-to-Process-Persistence or driver-to-Runtime
reference is needed. A registered product owner must supply the supported contract pair;
the query alone does not activate Process or Workflow executors or source policy.

Prove real SQL selected versus legacy denial, wrong execution/Agent/claim/creation time,
scope/stamp/contract/grant substitution, immutable-policy replacement, current-state
revocation with retained reads, restart, and bounded SQL excluding material payload.
Repeat related legacy source/decision tests and static/architecture gates. Actual
Workbench binding/publication, registered source policy, producer capture/enrollment,
atomic outcomes/accounting and manager continuation remain subsequent integration.
