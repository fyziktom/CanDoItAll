# default software development subprocess templates and agents

## Status

- `Completed`

## Objective

- Add default templates that split software development implementation into smaller subprocesses and ensure required agents/skills are available.

## Covered Inputs

- Main software development process is too large and should split implementation into subprocesses.
- Example steps include creating an empty solution from a Blazor SSR template and adding an xunit test project.
- Default templates and agents/skills must improve to support subprocess execution.

## Prerequisites

- `subbundles/01-architecture-source-of-truth-and-schema`
- `subbundles/02-runtime-subprocess-orchestration`
- `subbundles/04-canvas-and-editor-ui`

## Exact Source References

- `C:\repositories\CanDoItAll\Templates\Processes\manifest.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\software-delivery\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\toolbox\step-templates.json`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Templates\ProcessTemplatePackLoader.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Templates\ProcessTemplatePackModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Templates\ProcessTemplateEditorModelFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedBuilder.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\ProcessTemplatePackLoaderTests.cs`

## Deliverables

- A reusable `.NET implementation subprocess` template.
- Parent software delivery template references the subprocess where appropriate.
- Toolbox includes subprocess step seed.
- Template loading/import resolves subprocess process keys to process definition ids.
- Agent/skill seed updates only if needed by the new template.

## Dependency Impact

- Real scenario validation uses the new template to prove subprocesses can run alone and inside a parent process.

## Validation Depth

- `Template and process-critical validation`

## Implementation Steps

1. Extend template models for subprocess references by process key.
2. Add subprocess template folder and definition.
3. Update manifest and toolbox seed.
4. Update software delivery template to use subprocess step references.
5. Update import/projection tests.
6. Verify template pack loading and MCP template list.

## Scope Exceptions

- Do not fully rewrite all existing process templates.
- Do not add agents unless the template has a concrete role gap.

## Do Not Do

- Do not duplicate template process ids manually after import.
- Do not make subprocess references string-only at runtime.

## Acceptance Checklist

- Template pack loads.
- New subprocess template appears in template list.
- Imported parent template resolves subprocess target definition ids.
- Template steps are small enough for atomic execution and validation.

## Proof Required

- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\CanDoItAll.Mcp.Processes.Tests.csproj --no-restore --filter Template`
- Template import scenario proof.
- Execution report update.

## Browser Validation Logging

- Target route or window: process template library or process workspace after import.
- Required viewport passes: desktop.
- Required actions/assertions: confirm subprocess template is visible and parent step reference renders.
- Screenshot evidence: `process-subprocess-template-library.png`.
- Review questions: Is the default subprocess discoverable and specific enough?

## Progression Gate

- Continue only when template import produces strongly typed subprocess references.

## Suggested Agent Prompt

```text
Implement default subprocess process templates and import mapping. Keep runtime references as ids and use template keys only during template import.
```
