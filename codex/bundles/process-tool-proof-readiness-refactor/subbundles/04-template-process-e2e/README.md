# 04-template-process-e2e

## Status

- `Completed`

## Objective

- Migrate process templates to typed capability/proof contracts and validate the complete software-delivery QA proof flow end to end.

## Success Criteria

- Software-delivery QA validation and QA recheck steps declare required browser/runtime/image receipts as typed contract data.
- Screenshot/writeback templates declare their screenshot and image-analysis requirements as typed contract data.
- Process-scoped instruction fragments remain concise behavior guidance, not the only enforcement mechanism.
- E2E process validation proves either receipts are captured or the run stops with a typed readiness/fallback diagnostic.

## Covered Inputs

- R6 Template Migration.
- R1 through R5 full-system integration.
- Original domain leak concern around development/image-analysis instructions in common workspace tools.

## Prerequisites

- `01-runtime-receipt-contracts` completed.
- `02-hr-capability-readiness` completed.
- `03-manager-fallback-drivers` completed.
- New templates can be applied to the 5032 instance after implementation.

## Exact Source References

- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/dotnet-ui-screenshot-writeback/definition.json`
- `repo://Templates/Capabilities/mcps.json`
- `repo://Templates/Capabilities/tools.json`
- `repo://Templates/Capabilities/skills.json`
- `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplatePackLoader.cs`
- `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplateStepSummaries.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceRuntimePlugin.cs`
- `bundle://analysis/01-current-state.md`

## Deliverables

- Migrated process template contract fields for QA/browser/image proof steps.
- Template loader and summary support for typed contract fields.
- Template validation tests for software-delivery and screenshot/writeback processes.
- E2E validation plan and proof for the original `qa-recheck` failure mode.
- Confirmation that common MAF workspace image analysis prompts remain domain-neutral.

## Dependency Impact

- This is the closure phase for the bundle. Weak proof here reopens all prior subbundles.
- Future process templates depend on this migration pattern to add domain instructions without MAF leaks.

## Validation Depth

- End-to-end regression and closure.
- Requires build, targeted tests, template validation, process run proof, browser proof, and image analysis proof.

## Implementation Steps

1. Add template schema support for typed capability/proof contract fields if not already done in earlier phases.
2. Migrate software-delivery QA validation and QA recheck steps from prose-only proof requirements to typed contract data.
3. Migrate screenshot/writeback process proof requirements.
4. Keep process-scoped instruction fragments for human-readable behavior, but ensure enforcement comes from contracts and gates.
5. Rebuild/apply templates to the local 5032 instance during execution phase.
6. Run targeted unit and template tests.
7. Run an E2E process scenario that captures or correctly blocks browser/image proof.
8. Remove obsolete Calculator/Tetris artifacts only if the execution request includes cleanup and paths are verified safe.

## Scope Exceptions

- Do not migrate unrelated process templates unless they are required to prove the same contract mechanism.
- Do not add new common MAF domain prompt normalization.
- Do not treat a text-only final report as browser/image proof.

## Do Not Do

- Do not leave typed proof requirements duplicated only in prose.
- Do not require all agents to permanently lose development skills to make a management-only step work.
- Do not bypass readiness or receipt gates for convenience during E2E validation.
- Do not delete project artifacts without verifying the exact project-structure target paths.

## Acceptance Checklist

- `software-delivery` QA steps produce non-empty typed proof contracts.
- `dotnet-ui-screenshot-writeback` proof steps produce non-empty typed proof contracts.
- HR readiness recognizes required Playwright/image capabilities from the migrated templates.
- Runtime receipt gate requires the expected browser/image receipts.
- E2E validation captures browser screenshot and image analysis receipts or stops with a precise typed blocker.
- Common workspace image analysis code contains no software-delivery-specific prompt leak.

## Proof Required

- `dotnet build` for affected projects.
- `dotnet test` for template loader, contract compiler, readiness, receipt gate, fallback, and template migration tests.
- Template apply/rebuild transcript for the local instance during execution.
- E2E process run transcript with required receipt ids and artifact paths.
- Playwright screenshot artifact plus image-analysis receipt proof for the browser-visible flow.

## Browser Validation Logging

- Route: process run detail and any generated app/runtime URL used by the E2E process.
- Viewport: large desktop proof plus narrower-width follow-up if layout is affected.
- Playwright MCP evidence: `browser_navigate`, `browser_snapshot`, `browser_take_screenshot`, `browser_console_messages`.
- Screenshot evidence: record screenshot file names and image analysis receipt ids.
- Review questions: did the screenshot show the expected UI, did console output contain blocking errors, and did image analysis inspect the actual current-run screenshot.

## Progression Gate

- Bundle closure is allowed only when migrated templates, readiness, fallback, and receipt enforcement pass together in an E2E process run or produce an explicit typed external-environment blocker.

## C# Architecture Impact

- Completes the process-owned domain instruction channel and proves common MAF remains generic.

## Boundary Ownership

- Templates own domain requirements. Template loader parses them. Process application compiles them. MAF enforces generic runtime capability and receipt metadata.

## Dependency Direction

- Template code may reference process contracts. MAF workspace plugins must not reference process template definitions.

## Pattern Decision

- Use template data plus process-scoped instruction fragments. Do not use MAF prompt customization as the template policy mechanism.

## Testability Contract

- Template migration must be testable without the full 5032 instance, and E2E validation must prove the integrated runtime behavior separately.

## Partial Class Policy

- Do not expand UI or runtime partial classes with schema parsing or policy logic. Keep parsing in template services and decisions in process application services.

## Architecture Proof Required

- Include before/after template snippets, contract compiler output, and source proof of neutral MAF workspace prompts.
- Include E2E proof that required receipts are current-run receipts.

## Suggested Agent Prompt

```text
Implement subbundle 04 only. Migrate the software-delivery and screenshot/writeback templates to typed capability/proof contracts, apply the templates, run targeted tests and one E2E process proof, verify browser and image receipts, and confirm common MAF workspace prompts stay domain-neutral.
```
