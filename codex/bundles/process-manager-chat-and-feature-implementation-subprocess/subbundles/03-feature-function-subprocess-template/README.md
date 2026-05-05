# Feature Function Subprocess Template

## Status

- `Completed`

## Objective

Add a default `.NET feature/function implementation subprocess` and wire the existing .NET implementation slice to call it for bounded implementation work.

## Covered Inputs

- Add subprocess for implementation of functions/features.
- Use it in the main development process.
- Keep dispatcher generic and move details to process steps/agents.

## Prerequisites

- Existing template pack has software delivery and .NET implementation slice templates.
- Template import already supports subprocess references.

## Exact Source References

- `C:\repositories\CanDoItAll\Templates\Processes\processes\dotnet-development-slice\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\software-delivery\definition.json`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessSubprocessIntegrationTests.cs`

## Deliverables

- New template folder and definition.
- Updated template manifest/import order if required.
- Existing implementation slice delegates bounded coding to the new subprocess.
- Integration test covers nested template reference.

## Dependency Impact

- Software-delivery process gains more granular implementation without dispatcher-specific branching.
- Validation scenario depends on this template being importable.

## Validation Depth

- Targeted process module build.
- Integration test for default template import and nested subprocess resolution.

## Implementation Steps

1. Add new feature/function subprocess template files.
2. Wire the `.NET implementation slice` implementation step to the new subprocess.
3. Update template catalog metadata if needed.
4. Extend integration tests for import order and references.

## Do Not Do

- Do not hard-code .NET development behavior into runtime dispatcher services.
- Do not remove the existing implementation slice.

## Acceptance Checklist

- New subprocess imports successfully.
- Existing implementation slice references it by subprocess definition.
- Main software-delivery flow still references the implementation slice.

## Proof Required

- Integration test pass.
- Build proof for process module.

## Browser Validation Logging

- Browser proof optional unless template UI is opened.

## Progression Gate

- Continue only when nested subprocess references resolve deterministically.

## Suggested Agent Prompt

Add a default .NET feature/function implementation subprocess template and wire it into the existing .NET implementation slice. Prove import ordering and nested subprocess resolution with a targeted integration test.
