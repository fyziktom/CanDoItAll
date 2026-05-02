# Generic .NET And Blazor Agent Delivery Hardening

This bundle is a coordination and execution package for `generic-dotnet-blazor-agent-delivery-hardening`.

## Profile

- `initiative`

## Mission

Harden the default delivery agents, inline skills, and workspace tools so a process-run agent can build, run, and test generic .NET applications, with a dedicated Blazor specialist available for UI-heavy Blazor work. The implementation must remove sample-app-specific guidance from seeded agents and skills, expose generic startup proof tooling, and validate the result through two unrelated app-generation process runs under `C:\programovani\dotnet`.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report
- `inventories/` affected prompt, seed, tool, and validation surfaces
- `templates/` reusable validation notes

## Recommended Execution Order

1. `subbundles/01-01-agent-skill-tool-inventory`
2. `subbundles/02-02-dotnet-run-tooling`
3. `subbundles/03-03-generic-agent-and-blazor-specialist-seeds`
4. `subbundles/04-04-live-web-flow-validation`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.

## Validation Summary

- Bundle preparation status: `Prepared - validator passed`
- Execution status: `In progress`
- Subbundle gate review: `Subbundles 01-03 implemented and locally validated; subbundle 04 live validation in progress`
- Final closure gate: `Pending live process completion and final validator`
- Browser validation analytics: `Pending generated app outputs`

## Current Live Validation

- Web app: `http://localhost:5038`
- Ferry validation run: `8e3614ff-9bc1-499b-a1df-b29472e3c99c`, output root `C:\programovani\dotnet\FerryLostItemKiosk`
- Darkroom validation run: `b1c5e00f-e903-4863-b801-3e561f104009`, output root `C:\programovani\dotnet\CommunityDarkroomBookingBoard`
- Current result: both runs are still before the Blazor implementation step; no generated app source has been manually edited.
