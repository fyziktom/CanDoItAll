# Validation Checklist

## Preflight Validation

- Was `Tailwind/package.json` build run so `src/CanDoItAll.Components/wwwroot/css/output.css` reflects the current markup?
- Was the app validated from a live runtime, not from static code inspection alone?
- Were screenshots captured only after confirming the page was using the rebuilt CSS output?

## UX Validation

- Is the main task obvious within one screenful?
- Is the primary action visible without entering the first content card?
- Does each page explain what the user can do next?
- Does the page reduce cognitive load instead of just moving elements around?

## Layout Consistency Validation

- Does the route use the correct shell mode?
- Is there only one major page introduction?
- Does the page follow the shared page scaffold?
- Are list/detail pages using the same structural pattern?
- Are long forms broken into named sections?
- On protected focus-workbench routes, is the desktop main menu still visible while the workbench is docked?

## Responsive Validation

- Is navigation reachable below `lg`?
- Does the page remain usable on tablet widths?
- Do list/detail pages stack in a readable order on narrow screens?
- Do action rows wrap without breaking hierarchy?
- Does any right rail collapse or move predictably?

## Visual Hierarchy Validation

- Is the page header visually stronger than surrounding metadata?
- Are summary metrics above details when summary matters?
- Are secondary diagnostics visually weaker than task content?
- Are destructive actions visually separated from save/reset actions?

## Shared-Component Compliance Validation

- Is the route using the new shared composition primitives instead of new bespoke wrappers?
- Is page-local raw button/form/list markup reduced where a shared component now exists?
- Are status chips/badges following shared semantics?
- Are empty/loading states using the shared state components?

## Protected-Area Regression Validation

- Were protected workbench internals left behaviorally unchanged?
- Does dragging a structure group border move the selected nodes with it?
- Does double-clicking a structure progress badge reopen the progress preset menu?
- Do maximize/dock transitions still preserve usable canvas dimensions?
- In maximized mode, does the workbench host start at `left=0`, `top=0`, and match the viewport size instead of expanding inside an inner content card?
- Are maximized screenshots rejected if underlying shell cards or support rails still bleed through?
- Does the shared radial menu use the available hex space well enough that icons/labels are not visibly undersized or clipped?
- Does the priority submenu show numeric presets without duplicate text labels?
- Can the user zoom out to at least `15%` on the shared workbench surfaces?
- Do the following tests still pass?
  - `ProjectStructurePageTests`
  - `PromptFactoryPageTests`
  - `Direct_module_routes_and_workbench_surfaces_load_without_circuit_failure`
  - `Prompt_factory_canvas_surface_loads_and_exposes_shared_chrome`
  - `Structure_canvas_supports_inline_note_creation_editing_and_context_create_dialogs`

## Accessibility Sanity Checks

- Are all major actions keyboard reachable?
- Do forms retain visible labels?
- Are status indicators not color-only?
- Do compact controls still have readable text or accessible labels?
- Is focus order logical after shell and layout changes?

## Interaction Clarity Checks

- Can the user tell which list item is selected?
- Are click targets clearly "select", "open", or "secondary action" rather than ambiguous?
- Are save/run actions anchored and predictable?
- Are empty and no-results states clearly distinct?
- Do migrated filter bars expose the same expected task shortcuts on resources, test lab, and prompt gallery?
- Do settings and admin pages expose the missing top-level create actions without forcing deep drill-in first?
- On shared canvas routes, do toolbar actions remain uniquely targetable by accessible labels after shell/navigation changes?
