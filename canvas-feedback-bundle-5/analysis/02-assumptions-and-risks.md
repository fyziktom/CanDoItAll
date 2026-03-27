# Assumptions And Risks

## Assumptions

- Reusing the existing `CanvasFloatingWindow` chrome satisfies the request for minimize, hide, drag, and scroll behavior better than building page-local controls.
- The blocks explorer can keep its existing dark toolbox body styling while the outer shared window shell gains visible shared header controls.
- The screenshot validation can be satisfied through targeted Playwright coverage that captures the toolbox in both default and filtered or scrolled states.

## Risks

- Restoring the shared header without reducing the duplicate body copy will create a noisy double-title layout.
- Changing the floating-window shell can shrink the available vertical search area if the body overflow rules are not retuned carefully.
- Accordion behavior is already stateful in C#; mixing that with browser validation means the component and Playwright tests must agree on which groups are open.

## Required Guardrails

- reuse the shared floating-window component instead of inventing toolbox-only drag or window buttons
- keep the toolbox body dark and readable after the shared header returns
- prove the scrolled search state with browser screenshots, not only DOM assertions
