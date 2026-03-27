# 02 Add File Upload To Create Markdown Flow

## Objective

Let the markdown create flow accept uploaded markdown files without removing direct text entry.

## Covered Inputs

- `N002`
- `R002`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCanvasCatalog.RichDefinitions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCreateRequestComposer.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureCanvasCatalogTests.cs`

## Deliverables

- markdown create definition exposes the upload zone
- direct text fields remain available for typed markdown content
- accepted file types fit markdown usage

## Implementation Steps

1. Update only the markdown create definition.
2. Keep `ShowDefaultTextFields` enabled so typed content still works.
3. Add the file requirement and file prompt needed for markdown uploads.
4. Prove the definition through a focused test.

## Do Not Do

- do not change unrelated file create actions
- do not force users to upload a file when they only want to type markdown content

## Acceptance Checklist

- the markdown create action advertises upload support
- the markdown create action still uses the default title, subtitle, and notes fields
- accepted file types include markdown-capable files

## Proof Required

- focused component test pass
- execution report updated with the command and result

## Suggested Agent Prompt

```text
Implement subbundle 02 only.

Expose file upload for the markdown create flow by changing the typed catalog definition, but preserve direct text entry and avoid changing any other file action.
```
