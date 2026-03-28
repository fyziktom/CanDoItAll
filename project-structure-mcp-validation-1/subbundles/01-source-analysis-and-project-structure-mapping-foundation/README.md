# Source analysis and project-structure mapping foundation

## Status

- `Completed`

## Objective

- Convert the supplied source package into trusted analysis artifacts and lock the semantic mapping rules that will govern the live transfer into `CanDoItAll Main`.

## Covered Inputs

- `N001`
- `N002`
- `N003`

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\project-structure-mcp-validation-1\inputs\source-artifacts\CanDoItAllInput`
- `C:\repositories\CanDoItAll\project-structure-mcp-validation-1\inputs\source-artifacts\CanDoItAllInput.xmind`
- `C:\repositories\CanDoItAll\project-structure-mcp-validation-1\analysis\03-xmind-summary.json`
- `C:\repositories\CanDoItAll\project-structure-mcp-validation-1\analysis\04-xmind-outline.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.SharedKernel\ProjectObjectContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCanvasCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCanvasCatalog.RichDefinitions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureImportService.cs`

## Deliverables

- Source summary and outline artifacts derived from the supplied XMind package
- A bundle-level decision on which source branches become subprojects versus in-project blocks
- A documented mapping from source meaning to CanDoItAll node types and subtypes

## Dependency Impact

- Unblocks all live mutation work by defining the shape that later subbundles must create
- Prevents subbundle 03 from misclassifying large branches as flat work items

## Validation Depth

- Critical foundation
- Requires trusted source-analysis artifacts, explicit mapping rules, and a prepared validator pass before downstream work may continue

## Implementation Steps

1. Inspect the copied XMind package and generate reusable summary and outline artifacts.
2. Identify the large capability branches that should become subprojects under `CanDoItAll Main`.
3. Identify which remaining branches or leaves should become `ProjectBlock`, `WorkItem`, `Repository`, `File`, `Environment`, `Script`, `Infrastructure`, `Note`, or `Decision`.
4. Record the mapping and dependencies in the bundle documents.
5. Run the bundle readiness validator and repair any weakness before starting live mutation.

## Scope Exceptions

- This phase does not mutate the live CanDoItAll app yet.

## Do Not Do

- Do not create or link any live project nodes in this phase.
- Do not treat the generic XMind importer output as sufficient semantic mapping.

## Acceptance Checklist

- The source package is preserved in the bundle and also available as a valid `.xmind` archive.
- The generated analysis artifacts are present and referenced from the bundle.
- The bundle explicitly states which branches deserve subprojects and which remain typed nodes.
- The prepared-stage validator passes after this phase closes.

## Proof Required

- `C:\repositories\CanDoItAll\project-structure-mcp-validation-1\analysis\03-xmind-summary.json`
- `C:\repositories\CanDoItAll\project-structure-mcp-validation-1\analysis\04-xmind-outline.md`
- Successful `validate_bundle.py --stage prepared` output recorded in the execution report

## Browser Validation Logging

- `N/A`

## Progression Gate

- The prepared-stage validator passes
- The semantic mapping is explicit enough that another agent could create the live structure without guessing

## Suggested Agent Prompt

```text
Implement this subbundle only. Analyze the supplied XMind package, produce reusable bundle artifacts, and document the semantic mapping rules and project cut lines needed for the later live MCP import and shaping phases.
```
