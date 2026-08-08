# Architecture checkpoints

Each checkpoint is blocking. Copy its result into the corresponding subbundle proof folder and set the status explicitly.

## Common inputs

- Claude model/session and durable handoff state;
- CodeAnalytics snapshot ID and dashboard health when available;
- changed source and project files;
- responsibility inventory delta;
- before/after dependency graph;
- build and targeted test transcripts;
- source assertions and caller scans;
- negative/fault proof;
- old-owner shrink/deletion proof;
- cutover selector, telemetry, and rollback state;
- unresolved findings with severity and owner.

## CP1 — Context foundation (SB05)

Pass only when:

- live UI observation, conversation affinity, turn context, authority, execution run, and adapter state have separate named owners;
- Canvas -> Gantt next-turn behavior passes;
- an admitted Canvas run remains Canvas after navigation;
- Project X -> Project Y creates a new context epoch and authority resolution;
- navigation alone does not invoke a provider;
- UI access/scope cannot grant mutation authority;
- Gantt contributes bounded view facts without becoming canonical product truth;
- stable operational guidance no longer lives in volatile UI fragments.

Decision: `Unlocked`, `Blocked`, or `Unlocked with bounded follow-up`.

## CP2 — Scope and composition (SB08)

Pass only when:

- every scope-bound workspace service for one run has the same identity;
- no runtime/core class retains `IServiceProvider` or performs service location;
- no production fallback silently creates missing workspace/provider services;
- workspace construction no longer mixes a manual graph with root-container lookup;
- organization, project, sandbox, concurrent-project, and profile-switch tests pass;
- disposal/cleanup and primary-error preservation remain correct;
- runtime capability composition is testable without the full application host.

Decision: `Unlocked`, `Blocked`, or `Unlocked with bounded follow-up`.

## CP3 — Runtime split (SB11)

Pass only when:

- SDK-free narrow runtime ports exist;
- Core production callers use the appropriate ports;
- MAF execution, continuation, diagnostics, model administration, and hosted-agent construction are separate collaborators;
- the broad facade is delegation-only and has no new callers;
- direct unit tests do not instantiate the old facade;
- streaming, session, finalizer, tool, usage, cancellation, provider-error, and disposal behavior remains characterized;
- one production path is selected for each caller family.

Decision: `Unlocked`, `Blocked`, or `Unlocked with bounded follow-up`.

## CP4 — MAF/process boundary (SB14)

Pass only when:

- the MAF project has no `Modules.*` project reference or `using`;
- the MAF project no longer references `Workflows.MafAdapter` merely for handoff construction;
- no process outcome type, status, managed artifact path, source-kind branch, provider policy, or recovery behavior remains in MAF;
- process recovery and provider-selection/criticality policies are owned and directly tested by Processes;
- recovered output enters ordinary completion gates exactly once;
- stale/wrong/historical artifact evidence fails closed;
- dependency graph has no cycle.

Decision: `Unlocked`, `Blocked`, or `Unlocked with bounded follow-up`.

## CP5 — Integrated cutover stabilization (SB17)

Required before SB18:

- all high-risk scenario and fault matrices complete;
- one side-effecting production path per responsibility;
- runtime/context/authority/scope/provider correlation available;
- every discovered bug has an owner, failing regression test, and validated fix;
- no broad runtime bypass, service locator, mixed scope, process leak, current-context continuation, dual side effects, or lightweight-agent path;
- public projection and sensitive-data reviews pass;
- cleanup readiness and rollback report completed;
- durable Claude session handoff complete.

Decision: `Ready for cleanup`, `Blocked`, or `Ready with named compatibility readers retained`.

## CP6 — Final release gate (SB18)

Pass only when:

- CP5 permits cleanup;
- versioned runtime state and legacy compatibility fixtures pass;
- per-proposal decisions are authoritative and any retained reader/adapter is explicitly justified;
- workflow and future ordinary-chat foundations use the lightweight LLM port and cannot derive authority from data;
- obsolete writers, broad facades, fallback resolvers, selectors, and duplicate paths are deleted;
- named compatibility readers retained by CP5 remain read-only, bounded, and owned;
- full Release build and relevant unit/component/integration suites pass;
- manual Canvas/Gantt/floating/process/workflow/lightweight-LLM acceptance passes;
- canonical-model and C# architecture gates are `Pass`;
- no Critical or High architecture finding remains open.

Decision: final `Pass`/`Unlocked` or `Blocked`.

## Result format

```markdown
# Checkpoint result

Status: Unlocked | Blocked | Unlocked with bounded follow-up | Ready for cleanup | Ready with named compatibility readers retained | Pass

## Evidence

## Findings

| Severity | Finding | Evidence | Required action |
|---|---|---|---|

## Dependency direction

## Testability and fault proof

## Authority/source-of-truth/scope proof

## Cutover and rollback state

## Downstream decision
```
