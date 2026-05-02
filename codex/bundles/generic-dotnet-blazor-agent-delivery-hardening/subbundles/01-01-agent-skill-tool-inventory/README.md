# 01 Agent Skill Tool Inventory

## Status

- Status: `Completed`

## Objective

Inventory the active seeded agent, skill, tool, and test surfaces that control generic .NET/Blazor app delivery and identify sample-specific text that must be removed.

## Covered Inputs

- User request to analyze all default agents, their instructions, skills, and tools.
- User requirement to remove calculator/sample-specific hardcoding from generic process and agent cooperation.

## Prerequisites

- Bundle readiness validator has passed.
- Repository source is available locally.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\manifest.json
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\instructions\skills\blazor-ssr-delivery.md
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\instructions\agents\delivery-qa-observer.md
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Commands\WorkspaceCommandExecutionService.cs

## Deliverables

- Inventory notes in this bundle execution report.
- Source scan result identifying active sample-specific instructions and missing `workspace_dotnet_run` plumbing.

## Dependency Impact

- Subbundle 02 depends on this inventory to implement the correct generic tool surface.
- Subbundle 03 depends on this inventory to update only active seed guidance and tests.

## Validation Depth

- Source scans over seed assets, workspace command plumbing, MAF tool mapping, seed builder, normalizer, and integration tests.

## Implementation Steps

- Search active seeded instructions and skills for calculator, converter, and unit-topic hardcoding.
- Search command/tool plumbing for declared but unimplemented `workspace_dotnet_run`.
- Identify which seeded agents should receive run/build/test capabilities.
- Record any historical fixture names that are not active agent guidance.

## Scope Exceptions

- Historical test fixture names do not need removal unless they are asserted as active seeded guidance or leak into prompts.

## Do Not Do

- Do not repair generated validation apps.
- Do not move .NET/Blazor instructions into base process prompts.
- Do not remove legitimate framework-specific Blazor guidance from the Blazor specialist skill.

## Acceptance Checklist

- Active sample-topic guidance is enumerated.
- Missing run-tool plumbing is enumerated.
- Candidate files for generic seed and tool changes are identified.

## Proof Required

- Source scan commands and results.
- Inventory summary in `reviews/01-execution-report.md`.

## Browser Validation Logging

- N/A. This subbundle is source-inventory only.

## Progression Gate

- Downstream work may start only after the active prompt/tool gaps are known and no implementation target remains speculative.

## Suggested Agent Prompt

Inspect the exact source references, scan active seeded instructions and tool mappings for sample-specific app hardcoding and missing `.NET run` support, and report the concrete files that must change. Do not edit source in this subbundle.

