# Assumptions And Risks

## Assumptions

- A two-second delay is the intended "few seconds" because the existing opened-work floating cards use that value.
- The shared kernel is the correct home for the menu contribution contract because modules already reference it and should not depend on the Web project to describe shell navigation.
- `Workflows` should use `/agents/workflows` because the route already exists and the Agents page already navigates there.

## Critical Path Risks

- `02-module-navigation-contributions` is a critical foundation because a hardcoded Web-only item would fail the generic module requirement.
- `01-tooltip-delay-coverage` is a critical UI interaction foundation because eager tooltips can overlap menu flyouts.

## Validation Risks

- `ShellNavigation.MatchRoute` must see contributed items, or `/agents/workflows` could still highlight `Agents` instead of `Workflows`.
- Injecting an enumerable contributor service into the main layout must not break tests or routes when no modules contribute anything.
- If the available menu height is small, `Workflows` may be visible in the More panel rather than the standard row; browser proof should use a desktop viewport with the expanded menu and enough height to verify the intended order.

## Reopen Triggers

- Reopen tooltip work if Playwright finds a menu tooltip before the delay.
- Reopen navigation work if `Workflows` does not appear immediately after `Agents` in the merged navigation list.
- Reopen navigation work if `/agents/workflows` active matching resolves to `/agents`.
