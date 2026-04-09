# Implementation spec — PRM-F23

## Core implementation moves

- Define correlation models and bridge contracts inside Processes.
- Express registry ownership and permission narrowing in explicit process-side contracts.
- Reference the external AgentFramework repo only as a seam for future adapter alignment.
- Add tests that fail if the bridge tries to become a second canonical registry.

## Detailed expectations

1. Introduce or modify the listed repo touchpoints in the smallest coherent slice that can compile and test.
2. Keep comments in source code in English.
3. Prefer narrow bridge interfaces or projection services over broad cross-module coupling.
4. Preserve SQLite compatibility and keep PostgreSQL migration parity when storage is touched.
5. Honor Workbench projection-only guardrails whenever Workbench or Canvas surfaces are involved.

## Data and service notes

- Feature id: `PRM-F23`
- Canonical owner: `CanDoItAll.Modules.Processes`
- Cross-module touchpoints: PRM-F13, PRM-F16, PRM-F22

## Acceptance criteria

- Future external executor correlations can link ProcessRun, ProcessStepRun, and assignment records to runtime session, log, and metric identifiers.
- CRM-HR remains canonical for business role and agent templates plus durable AI identities even if runtime-level templates exist elsewhere.
- Shared provider and capability ownership is explicitly converged so the process bridge does not introduce a second canonical registry.
- Process step governance can narrow or require approvals for future AgentFramework permissions and external-call behavior.

## Suggested implementation order inside this feature

1. Add domain/contracts/entities first.
2. Add EF configuration and persistence tests.
3. Add application services and validation rules.
4. Add UI surfaces/adapters next.
5. Add end-to-end and regression coverage last.
