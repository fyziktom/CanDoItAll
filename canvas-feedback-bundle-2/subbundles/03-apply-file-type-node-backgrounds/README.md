# 03 Apply File-Type Node Backgrounds

## Objective

Make file nodes visually read as file-specific surfaces by strengthening the shared palette gradients.

## Covered Inputs

- `N003`
- `R003`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureGraphAdapter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\canvas-workbench.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureGraphAdapterTests.cs`

## Deliverables

- clearer shared gradients for file-node palettes
- readable text and badges after the change

## Implementation Steps

1. Keep the existing subtype-to-palette mapping in the graph adapter.
2. Tune the shared palette gradients so file nodes show white-to-color transitions that match their subtype cues.
3. Do not reduce contrast for body text, chips, or badges.

## Do Not Do

- do not add subtype-specific selectors per action id
- do not change palette keys in the graph adapter unless a test exposes a mapping bug

## Acceptance Checklist

- PDF-like nodes read as red or rose
- Excel-like nodes read as green or mint
- Word and markdown nodes read as blue or sky
- badges remain legible against the node background

## Proof Required

- focused component test pass for existing palette mapping
- browser or screenshot confirmation recorded in the execution report if available

## Suggested Agent Prompt

```text
Implement subbundle 03 only.

Strengthen the shared file-node palette gradients so subtype cues are visible at a glance, but keep the existing subtype-to-palette mapping and preserve readability.
```
