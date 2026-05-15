# 03 Office365 Processed Category And Template Settings

## Status

- `Completed`

## Objective

Make the Office365 email summary workflow mark the processed message and repair template settings so the generic Run Preview skip option is visible for the actual seeded Office365 workflow.

## Covered Inputs

- `N005`
- `N006`
- Requirements `R007`, `R008`, `R009`

## Prerequisites

- `01-oauth-connection-defaults` and `02-generic-project-storage-skip-preview` closure gates passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Office365\Office365GraphClient.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Office365\Office365WorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Office365\Office365BundledPlugin.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Office365\Office365PluginConstants.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Catalog\WorkflowTemplatePackLoader.cs`
- `C:\repositories\CanDoItAll\Templates\Workflows\workflows\default-workflows.yaml`
- `C:\repositories\CanDoItAll\Templates\Workflows\manifest.yaml`

## Deliverables

- Office365 mark-processed workflow executor.
- Microsoft Graph category mutation client that creates the processed category when missing.
- Office365 OAuth descriptor includes `Mail.ReadWrite` and `MailboxSettings.ReadWrite`.
- Default Office365 workflow stores the summary, preserves input payload, then marks the message processed.
- Gmail and Office365 download payloads carry the resolved OAuth connection id when executor settings leave `connectionId` blank.
- Workflow template scalar settings serialize as real JSON booleans/numbers.
- Template seed version bump refreshes managed default workflows.

## Dependency Impact

- This closes the Office365 reprocessing loop and fixes the concrete seeded-workflow preview gap that remained after the generic skip implementation.

## Validation Depth

- Critical workflow/runtime foundation.

## Acceptance Checklist

- Office365 message with `CanDoItAllSummaryTest` is moved to `CanDoItAllSummaryTestProcessed` after storage succeeds.
- Missing processed category is created before message patch.
- Existing Office365 connections without the new Graph scopes show reconnect-required.
- Actual Office365 template analysis returns a skip option for `store-office365-summary`.

## Implementation Steps

1. Add Office365 constants, settings models, and result models for processed category mutation.
2. Add Microsoft Graph client support for listing/creating Outlook master categories and patching message categories.
3. Add and register the Office365 mark-processed workflow executor.
4. Update the Office365 bundled plugin descriptor and OAuth scopes.
5. Update the default Office365 workflow template and bump the managed seed version.
6. Normalize YAML scalar settings in the workflow template loader.
7. Add targeted tests for Graph mutation, OAuth scopes, resolved connection-id payloads, and actual template skip detection.

## Do Not Do

- Do not mark an Office365 message processed before the project-structure summary storage step succeeds.
- Do not silently ignore missing Graph scopes; require reconnect through the OAuth status path.
- Do not hard-code preview skip logic to the Office365 workflow key.

## Proof Required

- Targeted Office365 client and OAuth integration tests.
- Targeted template preview unit test.
- Component tests for plugin login and workflow preview surfaces.
- Web build.

## Browser Validation Logging

- Existing browser proof from subbundle `02` covers the Project Structure surface and dialog shell.
- This subbundle is primarily backend/template behavior; the concrete Office365 template skip option is proven by unit/component tests because the live development fixture may not contain the exact seeded workflow node.

## Progression Gate

- Bundle may close after targeted tests prove Office365 category mutation, OAuth scope reconnect behavior, template seed refresh, and actual Office365 template skip detection.

## Suggested Agent Prompt

```text
Implement this subbundle only. Add Office365 processed-category mutation after summary storage, request the required Graph scopes, repair workflow-template scalar serialization so preview skip detection sees project-structure write operations, and validate with targeted integration, unit, component, and build proof.
```
