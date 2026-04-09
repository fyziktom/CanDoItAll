# Implementation spec — PRM-F16

## Core implementation moves

- Add a CRM-HR-owned role / agent template catalog with versioning.
- Let Processes reference a selected template and snapshot the chosen version.
- Link unresolved process roles to CRM-HR staffing / recruiting / AI sourcing flows.
- Expose eligible-pool and fallback metadata to runtime assignment services.

## Detailed expectations

1. Introduce the smallest coherent template catalog shape that supports human, AI, and hybrid staffing.
2. Reuse existing CRM-HR staffing and recruiting primitives where possible before inventing new workflow layers.
3. Keep comments in source code in English.
4. Preserve SQLite compatibility and keep PostgreSQL migration parity when storage is touched.
5. Honor Workbench projection-only guardrails whenever Workbench is part of the feature.

## Data and service notes

- Feature id: `PRM-F16`
- Canonical owners: `CanDoItAll.Modules.CrmHr` for templates; `CanDoItAll.Modules.Processes` for references/snapshots
- Cross-module touchpoints: PRM-F03, PRM-F07, PRM-F13

## Acceptance criteria

- A process actor role can optionally reference a reusable manager-approved role/agent template instead of free-text only.
- Templates capture modality, required skills/capabilities, allocation intent, and fallback/supervisory expectations.
- HR can open staffing, recruiting, or AI-agent sourcing work from unresolved process role gaps without losing process context.
- Published process versions snapshot the selected template version and key requirement summary.
- Runs snapshot the resolved assignee, eligible pool/fallback metadata, and rebind reasons.
- AI-oriented templates still resolve through CRM-HR identities and future execution bridges rather than direct runtime coupling.

## Suggested implementation order inside this feature

1. Add CRM-HR template entities / services first.
2. Add process-side reference and snapshot entities.
3. Add staffing brief link flows.
4. Add designer / editor UI.
5. Add runtime-readiness and integration coverage last.
