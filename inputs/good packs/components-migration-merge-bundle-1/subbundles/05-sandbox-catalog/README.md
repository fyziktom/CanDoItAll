# 05 Sandbox Catalog

## Objective

Create `CanDoItAll.Components.Sandbox` as the component catalog, demo lab, tuning surface, and visual acceptance gate for `Common`, `BaseLib`, and `CanvasLib`.

## Exact Source References

Shared inputs:

- `..\..\architecture\01-target-architecture.md`
- `..\..\inventories\02-componentkit-and-app-component-classification.md`
- `..\..\inventories\03-css-js-assets-and-tailwind.md`

Primary source components:

- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\Components`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components\Components`
- `C:\repositories\Zyphonote\src\App.Components\Radzen\Blazor`
- `C:\repositories\Zyphonote\src\App.Blazor\Components`

## Frontend Direction

Use the frontend skill principles, but adapt them for product UI rather than marketing UI.

- visual thesis:
  - a calm component laboratory with restrained surfaces, deliberate spacing, and visible behavior states
- content plan:
  - group index, focused demo surface, edge cases, prop variations, proof notes
- interaction thesis:
  - sticky section navigation, width/responsiveness toggles, subtle reveal on demo change

Avoid:

- dashboard-card mosaics as the catalog default
- decorative hero sections
- loud marketing copy
- multiple accent colors competing across groups

## Required Group Pages

- Foundations
  - typography
  - spacing/surfaces
  - icons
- Inputs
  - text inputs, numeric, dropdowns, checkbox, switch, password, text area
- Actions
  - buttons, button variants, inline actions
- Navigation
  - tabs, secondary tabs, steps, list/detail shells
- Feedback
  - alert, notification, tooltip, badges, status, empty, loading
- Layout
  - page header, page scaffold, form section, section card, split layouts
- Data Display
  - cards, lists, fact/meta tables, chips/pills
- Overlays
  - dialog, help popover, sticky footers, modals
- Canvas
  - workbench
  - calendar
  - floating windows
  - canvas primitives and overlays

## Required Demo Scenarios

- happy path
- dense content
- empty state
- loading state
- disabled state
- long text / truncation
- mobile width
- large desktop width
- keyboard/focus behavior where relevant

## Validation Questions

Agents validating the sandbox must ask all of these after taking screenshots:

- can I read all texts properly?
- will I like and understand this UI/layout as a new user?
- is there any too large component, gap, or visual disruption?
- do we use proper shared components instead of ad-hoc `div`/`span` structures?
- do we use available space properly?
- can the page be understood by scanning headings only?
- is the information hierarchy clear without decorative styling?
- do focus states, hover states, and disabled states read clearly?
- do sections feel intentionally composed rather than piled into cards?
- is any component depending on app-specific CSS that should not be in the sandbox?
- on mobile, does the first viewport still orient the user quickly?
- on desktop, are we avoiding dead horizontal space and accidental narrow content columns?

## Required Screenshot Proof

- desktop large: at least `1440px` wide
- mobile: around `390px` wide
- filename pattern:
  - `output/components-sandbox/<group>/<scenario>-desktop.png`
  - `output/components-sandbox/<group>/<scenario>-mobile.png`

If any answer is negative or uncertain, the implementing agent must tune the component composition, spacing, or variant usage before marking the scenario done.

## Acceptance Checklist

- every shared component group has a dedicated page
- fake data exists for complex cards/lists/canvas scenarios
- demos cover state variations, not just a single happy path
- screenshots exist for desktop and mobile
- the sandbox itself is usable as the future MCP example source

## Suggested Agent Prompt

```text
Implement subbundle 05 only.

Create CanDoItAll.Components.Sandbox as a Blazor Server component catalog for Common, BaseLib, and CanvasLib. Group the pages by function, keep the UX deliberate and product-like, and use fake data for richer demos. Treat the sandbox as a validation gate: capture desktop and mobile screenshots, answer the validation questions in the bundle, and tune any weak layouts before moving on.
```
