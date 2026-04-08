# Findings normalization and gap inventory

## Status

- `Ready`

## Objective

- Freeze the exact parity scope for this pass by mapping the Zyphonote findings and the earlier sibling parity bundles into one execution-grade inventory.

## Covered Inputs

- User request to prepare, execute, and validate a bundle from the Zyphonote findings.
- Zyphonote Finding 1: direct project-reference gap.
- Zyphonote Finding 2: focused-context member query failure.
- Existing sibling parity inventory for the already-closed symbol-tool gaps.

## Prerequisites

- none

## Exact Source References

- C:\repositories\zyphonote\bundles\2026-04-08-codeanalytics-vs-sharptools-evaluation\subbundles\03-codeanalytics-mcp-scenario-runs\findings\finding-01-project-reference-scenario-gap.md
- C:\repositories\zyphonote\bundles\2026-04-08-codeanalytics-vs-sharptools-evaluation\subbundles\03-codeanalytics-mcp-scenario-runs\findings\finding-02-focused-context-member-query-failed.md
- C:\repositories\zyphonote\bundles\2026-04-08-codeanalytics-vs-sharptools-evaluation\subbundles\02-scenario-ground-truth-and-benchmark-tasks\01-scenario-matrix.md
- C:\repositories\CanDoItAll.CodeAnalsis\CanDoItAll.CodeAnalsis.SymbolToolsBundle\inventories\01-missing-sharptools-surface.md
- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.CodeAnalytics\CodeAnalyticsTools.cs

## Deliverables

- Updated bundle documentation that defines the in-scope parity gaps.
- A parity inventory file under `inventories/`.
- A frozen dependency order for the implementation subbundles.

## Dependency Impact

- Every downstream subbundle depends on this inventory being accurate.
- If this phase misses a gap or mixes in an out-of-scope one, later implementation and rerun proof become noisy and untrustworthy.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Read the Zyphonote findings and scenario matrix.
2. Compare them with the current host MCP surface and the earlier sibling symbol-parity work.
3. Write the explicit parity inventory and lock the in-scope tool additions for this pass.
4. Update the bundle plan and traceability so later work can execute without guessing.

## Scope Exceptions

- Editing-tool parity is out of scope for this analysis-only bundle pass.

## Do Not Do

- Do not change product code yet.
- Do not invent new benchmark scenarios.
- Do not claim parity on capabilities that are not represented in the inventory.

## Acceptance Checklist

- The inventory clearly distinguishes already-covered symbol tools from still-missing parity gaps.
- The remaining subbundles match the frozen parity scope.
- The prepared-stage bundle validator can evaluate this subbundle without missing headings or source references.

## Proof Required

- `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\candoitall-codeanalytics-zyphonote-parity-bundle-v1 --profile initiative --stage prepared`

## Browser Validation Logging

- N/A

## Progression Gate

- The parity inventory is written, and prepared-stage bundle validation passes.

## Suggested Agent Prompt

```text
Normalize the Zyphonote findings into an execution-grade parity inventory. Do not change product code in this subbundle.
```
