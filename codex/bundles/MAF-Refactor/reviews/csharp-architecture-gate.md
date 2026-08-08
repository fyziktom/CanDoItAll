# C# architecture gate

Status: Pending

## Scope

Record the subbundles and commits under review.

## Required evidence

- [ ] CodeAnalytics snapshot and dependency/cycle result, or explicit availability gap
- [ ] Changed C# and `.csproj` files
- [ ] Responsibility inventory delta
- [ ] Target boundary map
- [ ] Pattern decisions
- [ ] Build transcript
- [ ] Unit/component/integration test transcript
- [ ] Source assertions
- [ ] Old-owner shrink/deletion proof
- [ ] Proof manifest
- [ ] Production caller-family migration inventory
- [ ] Single side-effecting path and rollback-selector proof
- [ ] Runtime-state compatibility fixture matrix
- [ ] Durable Claude session handoff for unfinished or retained compatibility work

## Findings

| Severity | Finding | Evidence | Required action |
|---|---|---|---|

## Responsibility result

- [ ] Each new type has one clear reason to change.
- [ ] Old owners no longer retain duplicate behavior.
- [ ] No broad Manager/Helper/Common dumping ground was added.

## Dependency direction

- [ ] Contracts do not reference implementations.
- [ ] Core does not reference product modules, UI, MAF, or provider SDKs.
- [ ] MAF has no product-module reference.
- [ ] No cycle exists.

## Construction

- [ ] No runtime/core service locator.
- [ ] No `BuildServiceProvider` in registration.
- [ ] Scope-bound services come from one typed factory.

## Testability

- [ ] Extracted behavior is tested without the old runtime.
- [ ] At least one negative test rejects a shallow implementation.
- [ ] Composition smoke proves the production path.

## Context and authority

- [ ] UI observation is not execution authority.
- [ ] Turn context is immutable after admission.
- [ ] Continuation retains original context and authority.
- [ ] Product switch creates a new context epoch and authority resolution.

## Cutover and state compatibility

- [ ] Exactly one side-effecting production path executes provider calls, tools, mutations, approvals, persistence, and process completion.
- [ ] Pure shadow comparison is limited to deterministic mapping/validation.
- [ ] Legacy runtime-state readers are retained only when named fixtures require them.
- [ ] New state is never silently reset, transcript-replayed, or downgraded.
- [ ] Every temporary selector/facade has telemetry, rollback semantics, and a deletion owner.
- [ ] Mock, scenario, diagnostic, API test host, and manual-composition paths use the same accepted seams.

## Lightweight LLM boundary

- [ ] Ordinary workflow LLM execution uses `ILlmInvocationPort`, not an agent runtime.
- [ ] LLM abstractions contain no MAF/provider SDK, agent/session, workspace, authority, tool, memory, approval, finalizer, process, or UI-context types.
- [ ] Provider-backed implementation reuses the provider runtime/driver stack exactly once.
- [ ] Usage, cancellation, response format, streaming terminal semantics, and sanitized failures have one owner.
- [ ] Future ordinary-chat contracts keep transcript persistence above the stateless port and do not construct a disabled agent.

## Partial-class policy

- [ ] No new partial class is used as a final architecture boundary.
- [ ] Any temporary partial has an owner and deletion subbundle.

## Closure decision

Status: Pass | Blocked | Pass with bounded follow-up

Downstream decision: Unlocked | Blocked
