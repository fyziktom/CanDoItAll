# screenshot-process-template-pack

## Status

- `Completed`

## Objective

Add reusable process templates for single-page screenshot capture and multi-page screenshot capture while keeping all screenshot-specific instructions in template data, steps, prompts, roles, and artifact expectations.

## Success Criteria

- `app-page-screenshot` exists in the template pack and can be listed/detailed/imported.
- `app-pages-screenshot-set` exists in the template pack and can be listed/detailed/imported.
- The multi-page template explicitly starts the app once, captures all pages, then stops/cleans up once.
- Templates define artifact expectations for screenshot files, browser evidence, review findings, and storage receipts.

## Covered Inputs

- R6, R7, R3.
- Raw note `N004`.

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\Templates\Processes\manifest.json`
- `C:\repositories\CanDoItAll\Templates\Processes\README.md`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\dotnet-development-slice\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\dotnet-development-slice\definition.md`
- `C:\repositories\CanDoItAll\Templates\Processes\shared\roles\software-engineer.json`
- `C:\repositories\CanDoItAll\Templates\Processes\shared\roles\qa-lead.json`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Templates\ProcessTemplatePackLoader.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ProcessesApi.cs`

## Deliverables

- Template directory `Templates\Processes\processes\app-page-screenshot`.
- Template directory `Templates\Processes\processes\app-pages-screenshot-set`.
- Manifest entries for both templates.
- Markdown docs, Mermaid diagrams, projection placeholders/reports, role/prompt/artifact/validation JSON where appropriate.
- Template import/list/detail proof.

## Dependency Impact

- Subbundle 05 depends on these templates for process-node linking and runtime proof.
- Missing artifact expectations would allow runs to finish without storing screenshots.

## Validation Depth

- `Critical process-template foundation`

## Implementation Steps

1. Copy the existing template-pack shape from a small process such as `dotnet-development-slice`.
2. Add single-page and multiple-page screenshot process definitions.
3. Add step notes that explain app startup, URL selection, Playwright MCP screenshot capture, console validation, image review, storage, and cleanup.
4. Ensure the multi-page process keeps one app process alive for all page captures.
5. Update `Templates\Processes\manifest.json`.
6. Run the app/template pack validation path or API template list/detail/import proof.
7. Update the execution report.

## Scope Exceptions

- Do not run Scenario 01 yet.
- Do not implement layout generation in this phase.

## Do Not Do

- Do not change process runtime to know about screenshots.
- Do not duplicate app-start/stop per page in the multi-page template.
- Do not create templates that rely on hidden agent memory instead of explicit artifact expectations.

## Acceptance Checklist

- [x] Single-page template is present in manifest.
- [x] Multiple-page template is present in manifest.
- [x] Multiple-page process has one startup and one cleanup phase.
- [x] Both templates declare screenshot and storage receipt artifacts.
- [x] Template detail/import proof is recorded.

## Proof Required

- Template-pack load/list command or API proof.
- Template import proof through `/api/processes/templates/{processKey}/import`.
- Diff audit showing process core unchanged for screenshot semantics.

## Browser Validation Logging

- N/A for browser UI. Runtime browser proof happens in subbundle 05.

## Progression Gate

- Subbundle 05 may start only after both templates can be listed, detailed, and imported.
- The multi-page template must clearly avoid per-page app restarts.

## Suggested Agent Prompt

```text
Implement only the screenshot-process-template-pack subbundle.
Add process-template-pack entries for single-page and multi-page app screenshots. Put all Playwright and app-start instructions in template steps and prompts, not process core. Validate template loading/import and update the execution report.
```
