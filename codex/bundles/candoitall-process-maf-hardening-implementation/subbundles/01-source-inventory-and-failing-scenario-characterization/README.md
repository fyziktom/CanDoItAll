# SB01 - Source Inventory And Failing Scenario Characterization

## Status

- `Completed`
- Critical foundation: yes

## Objective

Create the implementation-time baseline: exact current source inventory, test inventory, template/artifact inventory, and failing scenario characterization for the blocked `prepare-solution-skeleton` class and every similar subprocess/template/artifact contract surface.

## Covered Inputs

- User request to analyze all similar process/template/artifact trouble.
- GPTPro F01-F12 as inventory inputs.
- GPTPro evidence about calculator output containing product files but missing managed process evidence.
- Local inventory of nine subprocess parent steps.

## Prerequisites

- Prepared bundle files are present.
- CodeAnalytics MCP available or explicit validation gap recorded.

## Exact Source References

- `bundle://inputs/gptpro-analysis-source/data/findings.json`
- `bundle://inputs/gptpro-analysis-source/data/source-map.csv`
- `bundle://inputs/gptpro-analysis-source/evidence/calculator-output-inspection.md`
- `repo://Templates/Processes/processes`
- `repo://Templates/Processes/shared/artifacts`
- `repo://src/Processes`
- `repo://src/Modules/CanDoItAll.Modules.Processes`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs`
- `repo://src/MAF/Common`
- `repo://tests`

## Deliverables

- Updated source/test/template inventory in `inventories/`.
- Exact failing scenario notes for `prepare-solution-skeleton`.
- Confirmed list of all subprocess parents and child terminal outcomes.
- Current test coverage map and missing test list.
- Implementation-time CodeAnalytics snapshot or explicit blocker.

## Dependency Impact

- Every later subbundle depends on this inventory. Missing a parent step, child no-go path, shared artifact hard gate, or current test surface can invalidate SB04-SB09.

## Validation Depth

- Critical foundation.
- Requires CodeAnalytics evidence, template parser output, source references, and test discovery.

## Implementation Steps

1. Re-run structured parsing of `Templates/Processes/processes/*/definition.json` for subprocess parents, manual skip, artifact expectations, child mappings, capability scope, required receipts, and long prose gates.
2. Parse `Templates/Processes/shared/artifacts` for artifact templates that encode handoff, proof, screenshot, runtime-command, QA, review, implementation, or escalation hard gates in markdown.
3. Search current source for GPTPro-cited symbols and update stale paths, including the project-structure provider path under `CanDoItAll.Modules.Workbench`.
4. Build a narrowed CodeAnalytics snapshot for source areas that will be edited.
5. Locate existing tests by symbol and project.
6. Create characterization test plan rows for old behavior that must fail after implementation.
7. Update `reviews/01-execution-report.md` with inventory command transcripts.

## Scope Exceptions

- Do not require live 5032 process access in SB01. Record whether it is available; SB09 owns live recovery proof or blocker.

## Do Not Do

- Do not edit production source.
- Do not normalize away GPTPro finding scope.
- Do not skip shared artifact templates just because the example was a process definition.

## Acceptance Checklist

- [x] All nine known subprocess parent steps are listed with parent expectation, accepted child outputs, and no-go outputs.
- [x] Shared artifact template audit scope is listed with edit/follow-up decisions.
- [x] Current tests and missing tests are enumerated.
- [x] CodeAnalytics snapshot id, scope, health, findings/hotspots, and dependency cycles are recorded.
- [x] Stale GPTPro source paths are corrected in inventory.

## Proof Required

- `proof/SB01/manifest.md`
- `proof/SB01/semantic-invariants.md`
- Transcript for template inventory command.
- Transcript for source/test inventory command.
- CodeAnalytics snapshot id and dependency result.
- Anti-stub audit is not required yet unless production files are changed, but record TODO/NotImplemented hotspots if found.

## Browser Validation Logging

- `N/A` unless live app UI is used to inspect the blocked process. If used, record route/window, viewport, screenshot, and action result in `reviews/01-execution-report.md`.

## Progression Gate

- Downstream implementation may start only when the inventory covers GPTPro F01-F12, all nine subprocess parents, shared artifact template audit scope, current source refs, and test gaps.

## C# Architecture Impact

This phase establishes the architecture baseline and changed-file candidates.

## Boundary Ownership

No ownership changes yet. Record likely owners for each later extraction.

## Dependency Direction

Record current project references and CodeAnalytics cycles. No source reference changes allowed.

## Pattern Decision

No patterns are implemented in SB01. Validate that planned patterns in `architecture/03-csharp-pattern-selection-records.md` still fit current source.

## Testability Contract

Identify direct unit-test seams for each planned service and the tests that currently force full large-class construction.

## Partial Class Policy

Inventory existing partial clusters and record where later phases must avoid adding final logic.

## Architecture Proof Required

- CodeAnalytics snapshot evidence.
- Source inventory with large class/partial risks.
- Test seam inventory.

## Suggested Agent Prompt

```text
Execute SB01 only. Build the current source, template, artifact, and test inventory. Do not edit production code. Update proof/SB01 and execution-report rows. Stop if the template inventory cannot account for every subprocess parent or if CodeAnalytics cannot provide usable evidence.
```
