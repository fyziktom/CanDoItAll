# 01 Foundation And Governance

## Objective

Create the new project skeleton, dependency rules, ownership rules, and request workflow before any component code is moved.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll.slnx`
- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\CanDoItAll.ComponentKit.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components\CanDoItAll.Components.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`
- `C:\repositories\Zyphonote\src\App.Blazor\Zyphonote.App.csproj`
- `C:\repositories\Zyphonote\src\App.Components\Zyphonote.App.Components.csproj`

## Deliverables

- new project entries for:
  - `CanDoItAll.Components.Common`
  - `CanDoItAll.Components.BaseLib`
  - `CanDoItAll.Components.CanvasLib`
  - `CanDoItAll.Components.Sandbox`
  - `CanDoItAll.Mcp.Components`
- a clear dependency direction matching `architecture/01-target-architecture.md`
- shared-library request folder convention owned from CanDoItAll
- a short contributor rule set that forbids direct shared-lib edits from Zyphonote work unless the work is happening in CanDoItAll

## Implementation Steps

1. Add the new projects to `CanDoItAll.slnx`.
2. Keep old projects compiling during transition; do not delete `CanDoItAll.ComponentKit` or `CanDoItAll.Components` yet.
3. Establish the target package/reference graph first.
4. Create empty `Requests` folders in the future shared projects:
   - `src\CanDoItAll.Components.Common\Requests`
   - `src\CanDoItAll.Components.BaseLib\Requests`
   - `src\CanDoItAll.Components.CanvasLib\Requests`
5. Add a short `README.md` inside each request folder explaining:
   - shared libs are owned from CanDoItAll
   - Zyphonote or other repos must file a request instead of patching shared libs directly
6. Add an initial request template from `templates/change-request-template.md`.
7. Add solution-level notes that preview/demo components must live in sandbox, not runtime libs.

## Do Not Do

- do not move any existing component code in this phase
- do not rename namespaces in app pages yet
- do not create compatibility wrappers yet
- do not touch Zyphonote code beyond the plan-driven future phase

## Acceptance Checklist

- all new projects exist and build as empty/minimal skeletons
- references match the target dependency direction
- no old runtime dependency cycle is made worse
- request workflow exists in the CanDoItAll repo
- an implementation agent can tell where to place a shared-library change request without guessing

## Proof Required

- solution file diff showing new projects added
- build output for the new skeleton projects
- screenshot or text proof of the request folder layout

## Suggested Agent Prompt

```text
Implement subbundle 01 only.

Create the target shared-component project skeleton in CanDoItAll without moving runtime code yet. Keep changes minimal and structural. Add the request/governance folders for Common, BaseLib, and CanvasLib so future work from Zyphonote cannot edit shared libs directly. Do not start wrapper migration, canvas extraction, or app rewiring in this phase.
```
