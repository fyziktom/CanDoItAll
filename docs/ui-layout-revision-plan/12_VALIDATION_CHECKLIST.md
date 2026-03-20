# Validation Checklist

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
