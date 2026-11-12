# Inherited planning in the workspace lifecycle

Status: implemented with [185 passing selected cases](../validation/2026-10-19-inherited-workspace-lifecycle.md),
following signed canonical-reader checkpoint 44ab86c7a. The design below preceded implementation.
Process/Workflow producers remain disabled until their source, publication and outcome owners are wired.

## Ownership and dependencies

Expose a narrow in-process inherited-workspace port in AgentFramework Core. Its request
contains an exact child execution ID, Agent, original input/source context, activity ID,
registered planning owner, original-parent validator and new-only binding callback.
It is not an HTTP DTO. The caller is the owning integration adapter; its callbacks cannot
replace the journal's current source checks or the registered participant/runtime contract.
The admitted logical context scope is explicit and may be a Project inside organization
storage, as with interactive execution. Canonical parent/source checks own its validity;
retries must preserve it. Do not confuse the storage scope with history attribution.

A cohesive inherited execution service owns exact admission, retained disposition and
entry into the planning coordinator. Reuse existing preparation/provider selection and
completion through typed delegates and a moved internal startup value. The existing
workspace facades only route calls. Add no runtime partial, product-module dependency,
new history scan, persistence index or duplicate provider/credential lifecycle.

The coordinator acquires the live journal owner before entering the completion handler.
One opaque, noncopyable execution capability owns provider entry. Its lease remains bound
through credentials, provider work, usage/failure persistence and cleanup. A competing
caller rejected during startup cannot enter the handler that writes terminal failures.
The shared provider loop remains common to both origins; an inherited capability can run
it only once and is invalid outside its owner's lifecycle.

## Retry and failure behavior

Exact lookup and canonical parent/read validation precede deferred preparation. A new
candidate is admitted once. A retained, never-started initial decision may start using
its saved BackgroundInput, source context, model and provider identity. Changed incoming
prompt/context cannot replace those values; current credentials and the registered runtime
contract must still validate. Never replay an open/unknown interval, waiting proposal or
terminal run. Return those original records explicitly without new work or grant.

Returning retained data completes only that caller's activity, after reporting its
PersistingResult phase. It does not terminalize, cancel or mutate the retained execution.

An already persisted child must be read and authorized without taking its provider
dispatch lease. Otherwise a harmless retry blocks behind the provider and can deadlock
an orchestrator waiting for the retained result. New admission still takes that lease
and repeats the exact lookup inside it, preserving one initial commit under races.

Acquire a dispatch owner before the existing failure-writing handler and hold it until
that handler completes. Preparation/startup rejection leaves the original run untouched.
Errors after ownership preserve the existing usage, cancellation and failure behavior;
unknown work stays recorded and is not refunded or restarted. Do not add speculative
elapsed accounting on failure. Source read revocation still prevents result disclosure.

Inherited detail reads use current source and participant authorization without invoking
the interactive chat review owner. Manager approval remains unsupported in this slice.
Existing generic/interactive entries retain their original guards and semantics.

## Validation

Exercise the registered workspace facade and real MAF SDK through intercepted HTTP,
including successful preparation/usage/activity completion, original input/scope,
retained initial retry, provider/participant contract changes, duplicate callers,
unknown provider work, retained waiting/terminal records and current-read denial.
Prove the live capability is single-use and does not outlive its lifecycle. Retain the
existing interactive entry, shared coordinator, inherited admission and architecture
regressions. Build changed production projects in standard Release, verify exact focused
discovery, run complete portability-static and before/after scoped dependency review.
Reuse existing test families and the isolated PostgreSQL fixture; record source/runtime
identity and resource closure. Product-source and manager authority are subsequent gates,
not claims established by a test source policy.
