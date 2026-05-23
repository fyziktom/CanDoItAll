# SB03: Generic Blazor Process Template Pack

## Status

- Status: `Completed`
- Critical foundation: `Yes`

## Scope

- Add reusable process templates for Blazor application work.
- Keep Blazor and UI proof requirements in templates, steps, roles, prompts, and artifacts.
- Avoid adding Blazor or Tetris branching to process runtime code.

## Objective

Make CanDoItAll able to launch generic Blazor app delivery, repair, and feature-addition processes where agents must build and validate the app.

## Covered Inputs

- Follow-up request `03-live-blazor-delivery-request`
- `R007`
- `R008`

## Prerequisites

- SB01 and SB02 remain complete.
- Process template pack loads from `Templates/Processes`.

## Exact Source References

- `repo://Templates/Processes/manifest.json`
- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/app-pages-screenshot-set/definition.json`
- `repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackLoader.cs`
- `repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplateProjectionService.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`

## Dependency Impact

- Template-pack data and template projection tests.
- No process runtime branching by Blazor, Tetris, or app type.

## Validation Depth

- Template pack load.
- Projected envelope checks for all new template keys.
- Required evidence-contract term assertions.

## Required Edits

- Add `blazor-app-delivery` for new Blazor SSR, WASM, or WASM PWA application delivery.
- Add `blazor-app-repair-fix` for diagnosing and repairing an existing Blazor app.
- Add `blazor-backend-feature`, `blazor-frontend-feature`, and `blazor-fullstack-feature` for feature additions.
- Reuse shared process roles and artifact resources where possible.
- Add only template-pack JSON/Markdown and tests unless a generic template loader issue is discovered.

## Implementation Steps

- Add five process template folders and manifest entries.
- Reuse shared roles and artifacts where possible.
- Add QA/runtime proof artifact expectations to each template.
- Add compact evidence index and self-review summary requirements.
- Add integration tests for template projection and required proof terms.

## Do Not Do

- Do not encode Tetris-specific rules.
- Do not add process runtime special cases for Blazor.
- Do not make browser proof optional for visible Blazor UI surfaces.

## Acceptance Checklist

- [x] `blazor-app-delivery` template loads and projects.
- [x] `blazor-app-repair-fix` template loads and projects.
- [x] `blazor-backend-feature` template loads and projects.
- [x] `blazor-frontend-feature` template loads and projects.
- [x] `blazor-fullstack-feature` template loads and projects.
- [x] QA/release steps require screenshots, console evidence, and project-structure writeback.

## Proof Required

- `bundle://proof/SB03/manifest.md`
- `bundle://proof/SB03/transcripts/template-tests.txt`

## Browser Validation Logging

- Not applicable to template authoring itself. Browser proof is required in the processes authored by this subbundle and validated in SB07.

## Progression Gate

- SB03 passes when template tests pass and prepared templates project successfully.

## Suggested Agent Prompt

Use `bundle://shared-prompts/implementation-prompt.md`.

## Contract

Every template must require:

- project-structure source-of-truth input
- explicit app mode: SSR, WASM, or WASM PWA
- output root and run folder recording
- `dotnet restore`, `dotnet build`, and relevant tests
- one local app startup receipt
- Playwright/browser navigation for visible UI surfaces
- screenshot image artifacts
- browser console output proving no active JavaScript/runtime errors
- URL or entrypoint evidence
- cleanup receipt
- project-structure result/evidence writeback
- compact evidence index and self-review summary

## Acceptance

- Template pack loads successfully.
- Projected envelopes for all five templates succeed.
- Tests assert required proof language is present in the relevant QA/release steps.
