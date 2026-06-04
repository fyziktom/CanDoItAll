# Large-Screen-Only Proof Policy

This bundle is runtime/service/architecture work. Browser validation should be `N/A` unless an implementation unexpectedly touches a rendered UI route.

If browser validation becomes necessary:

- Use only PC / large-screen validation.
- Do not run small-screen, medium-screen, or mobile viewport checks.
- Do not produce mobile screenshots.
- Do not spend time optimizing responsive/mobile layouts.
- Use a large desktop viewport or maximized headed browser.
- Record route, viewport, assertions, and screenshot path only for large-screen proof.

Reason: the current product target for this work is PC/large-screen usage. Mobile screenshots caused wasted proof effort in the previous cycle.
