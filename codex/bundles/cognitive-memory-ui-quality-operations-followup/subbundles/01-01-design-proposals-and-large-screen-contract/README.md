# 01-design-proposals-and-large-screen-contract

## Status

- `Completed`

## Completion Evidence

- Imagegen proposal files are preserved under `inputs/imagegen`.
- Large-screen-only rule is recorded in the bundle requirements, architecture, and execution report.
- Component MCP was unavailable during execution, so the UI pass used local source inspection and existing BaseLib component patterns.

## Objective

Preserve the imagegen UI proposals and convert the user's large-screen-only rule into enforceable implementation constraints.

## Covered Inputs

- UI-01, UI-02, UI-12, UI-14.

## Prerequisites

- Bundle root exists and image proposals have been generated.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-ui-quality-operations-followup\inputs\imagegen\proposal-overview.png`
- `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-ui-quality-operations-followup\inputs\imagegen\proposal-tabs.png`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.css`

## Deliverables

- Imagegen artifacts are preserved in bundle inputs.
- Large-screen-only rule appears in requirements, architecture, and execution report.
- Existing medium/small Cognitive Memory CSS tuning is identified for removal in Subbundle 04.

## Dependency Impact

- All downstream UI work must follow this contract.

## Validation Depth

- Process-critical.

## Implementation Steps

1. Verify proposal files exist.
2. Verify the bundle records the hard rule.
3. Record the component MCP fallback note.

## Do Not Do

- Do not treat imagegen proposals as shipped UI proof.
- Do not introduce medium or small viewport requirements.

## Acceptance Checklist

- Proposal images are in bundle inputs.
- Large-screen-only rule is explicit.
- Browser proof scope is large desktop only.

## Proof Required

- Prepared-stage bundle validator.

## Browser Validation Logging

- N/A for this subbundle; imagegen is planning input only.

## Progression Gate

- Subbundle 02 may proceed only after the hard rule is documented.

## Suggested Agent Prompt

```text
Validate subbundle 01 only. Preserve imagegen artifacts and ensure the large-screen-only rule is explicit before data contract work starts.
```
