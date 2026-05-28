# 03-tailwind-and-documentation

## Status

- `Completed`

## Objective

- Split Tailwind ownership into component-library output from the components repo and CanDoItAll-specific output from the main repo, wire the main app to load both, and document the workflow.

## Covered Inputs

- REQ-007, REQ-009.

## Prerequisites

- SB02 completed so the main repo has package consumption in place.

## Exact Source References

- `repo://Tailwind`
- `C:/repositories/CanDoItAll.Components`
- `repo://src/CanDoItAll.Web/Components/App.razor`
- `repo://tools/CanDoItAll.Manager/ManagerOptions.cs`
- `repo://tests/CanDoItAll.Tests.Unit`
- `repo://README.md`
- `repo://docs`
- `C:/repositories/CanDoItAll.Components/README.md`

## Deliverables

- Components repo Tailwind builds BaseLib package output.
- Main repo Tailwind builds `src/CanDoItAll.Web/wwwroot/css/output.css`.
- Web app loads both `_content/CanDoItAll.Components.BaseLib/css/output.css` and app-specific output.
- Manager defaults/tests are updated to the new main Tailwind output path.
- Documentation in both repos explains package and Tailwind build steps.

## Dependency Impact

- SB04 build/browser validation depends on both CSS outputs existing and being referenced in the correct order.

## Validation Depth

- UI-supporting build and documentation validation.

## Implementation Steps

1. Keep component-library CSS imports and scanning in the components repo Tailwind workspace.
2. Keep CanDoItAll-specific CSS imports and scanning in the main repo Tailwind workspace.
3. Update root npm scripts and Tailwind README files in both repos.
4. Add app-specific CSS link after component CSS in the main web app.
5. Update manager Tailwind output defaults and tests.
6. Build both Tailwind outputs.

## Scope Exceptions

- Do not visually redesign components or pages.

## Do Not Do

- Do not keep all Tailwind source in the main repo.
- Do not make the main app rely only on package CSS when main-specific classes still exist.
- Do not introduce a new frontend framework.

## Acceptance Checklist

- Component Tailwind build produces BaseLib `wwwroot/css/output.css` in the components repo.
- Main Tailwind build produces `src/CanDoItAll.Web/wwwroot/css/output.css`.
- Web app includes both CSS outputs.
- Docs list build/watch commands for both repositories.

## Proof Required

- Tailwind build transcripts for both repos.
- Source assertions for CSS link order and Tailwind package scripts.
- Documentation source assertions.
- Browser proof on `/` when the app can start; explicit blocker if it cannot.

## Browser Validation Logging

- Target route: `/` in `CanDoItAll.Web` after successful build.
- Viewports: desktop first, narrow viewport if browser proof is reachable.
- Required assertions: both CSS links are present in document head and HTTP/static asset resolution succeeds.
- Screenshot: `proof/SB03/browser-home-css.png` when reachable.

## Progression Gate

- Pass when both Tailwind builds succeed and source assertions prove the two-output model is wired and documented.

## Suggested Agent Prompt

```text
Implement SB03 only. Split Tailwind source and outputs, wire the main app to load component and main CSS, update documentation, and capture build/source proof.
```
