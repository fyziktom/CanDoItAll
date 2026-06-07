# process-core-narrow-seed-route-rules-driver-proposal-prep-v1

Status: Completed.

## Goal

Create the first **narrow Process Core seed** only for pure route-stage and route-eligibility read models/rules, while keeping all process orchestration, claims, EF, workspace/storage/filesystem, AgentFramework execution, finalizer application, and helper-driver runtime APIs out of scope.

This bundle deliberately **does not perform a broad Process Core split**. It introduces only the smallest Core cutline that the latest branch evidence says is ready, and it keeps future driver work as a proposal/documentation lane.

## Why this bundle exists

The latest `maf-processes-refactor` branch proof says:

- the prior pre-Core consolidation completed,
- no production Core project or production driver API exists yet,
- the final red-team says the next step may be a narrow Core proposal,
- broad extraction is still blocked by EF, workspace/storage, filesystem, AgentFramework, claim lifecycle, transitions, and finalizer coupling.

## Non-negotiable constraints

- Do not move process orchestration into Core.
- Do not move route handlers, route services, dispatch claim lifecycle, transition execution, finalizer application, EF queries, workspace/storage/file IO, AgentFramework execution, provider repair, retry, or artifact projection into Core.
- Do not introduce production process-driver APIs, registries, DI registrations, runtime selectors, manager commands, or execution-capable helper drivers.
- Browser/UI/mobile/small/medium proof is N/A unless UI files are unexpectedly touched; unexpected UI changes must fail the bundle.
- Preserve all existing process behavior. This is architecture hardening and dependency isolation, not feature removal.

## Implementation profile

Profile: initiative.

Expected implementation branch: `maf-processes-refactor`.

Primary production area:

- `src/CanDoItAll.Modules.Processes`
- new narrow project: `src/CanDoItAll.Processes.Core`
- solution file updates
- unit/integration tests and architecture guardrails

## Bundle structure

- `analysis/` — current-state review and cutline reasoning
- `requirements/` — hard constraints and acceptance criteria
- `architecture/` — Core seed design and driver proposal lane
- `plan/` — dependency-aware phase plan
- `subbundles/` — 30 larger subbundles grouped into 10 phases
- `evidence/checklists/` — XLSX execution checklist
- `reviews/` — execution report template
## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Passed`
- Browser validation analytics: `N/A - no UI/media files changed`
