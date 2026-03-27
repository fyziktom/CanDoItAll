# Test Coverage Plan

## Principle

Reuse the existing test projects first. Do not split tests into new projects unless the current structure becomes unworkable.

## Shared Library Coverage

### `CanDoItAll.Components.BaseLib`

Primary test home:

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components`

Required coverage:

- wrapper rendering basics
- keyboard and focus behavior
- disabled/loading/empty states
- icon resolution and local asset fallback behavior
- tabs, steps, and form input state transitions
- generic surface components promoted from `ComponentKit` and `App.Blazor`

### `CanDoItAll.Components.CanvasLib`

Primary test home:

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components`

Required coverage:

- canvas contract parsing and serialization
- runtime component rendering
- JS interop boundary behavior through harnesses
- selection, layout, tooltip, clipboard, overlay, and calendar flows already covered today
- asset path validation after extraction

### `CanDoItAll.Mcp.Components`

Recommended test home:

- a dedicated test project next to the MCP server, or extend an existing MCP test project if the patterns align

Required coverage:

- tool registration
- search/index behavior
- example lookup
- component metadata resolution
- read-only guarantees

## App Adoption Coverage

### CanDoItAll

Reuse:

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright`

Required additions:

- smoke coverage for pages using new `BaseLib` surfaces
- smoke coverage for pages using extracted `CanvasLib`
- asset resolution checks for new `_content/...` paths

### Zyphonote

Reuse:

- `C:\repositories\Zyphonote\tests\App.Web.PlaywrightTests`
- `C:\repositories\Zyphonote\tests\App.PdmxTool.PlaywrightTests`

Required additions:

- marketplace/account page regressions after shared wrapper adoption
- planning/canvas page regressions after `CanvasLib` path changes
- screenshot assertions or artifact capture for visually sensitive surfaces

## Sandbox Coverage

Recommended minimum:

- route smoke tests for each sandbox group page
- bUnit checks for fake-data demo hosts where logic exists
- Playwright screenshot capture flow for desktop and mobile

## Exit Rule

No phase that changes shared component behavior is complete without:

- build proof
- relevant unit/component test proof
- relevant Playwright proof where UI changed
- screenshot proof where layout or styling changed
