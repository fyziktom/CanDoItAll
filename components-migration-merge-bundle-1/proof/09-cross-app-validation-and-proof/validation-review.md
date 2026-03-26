# 09 Validation Review

## Evidence

- Validation date: 2026-03-25
- Build logs: `logs/candoitall-build.log`, `logs/zyphonote-build.log`
- Test logs and TRX: `logs/candoitall-components-tests.log`, `logs/candoitall-playwright.log`, `logs/zyphonote-web-playwright.log`, `logs/zyphonote-pdmx-playwright.log`, `logs/trx/*`
- Screenshot manifest: `screenshots/manifest.json`
- Screenshot inventory reviewed: 58 PNG files total
- Proof capture diagnostics: `logs/capture-proof-run.log`, `logs/capture-proof-screens.stdout.log`, `logs/capture-proof-screens.stderr.log`

## Build And Test Summary

- CanDoItAll build: success, 4 existing `NU1510` warnings in `CanDoItAll.Mcp.DotNetWatch`, 0 errors.
- Zyphonote build: success, 0 warnings, 0 errors.
- CanDoItAll shared component tests: 132 / 132 passed.
- CanDoItAll Playwright: 7 / 7 passed.
- Zyphonote web Playwright: 77 / 77 passed.
- Zyphonote PDMX Playwright: 15 / 15 passed.

## Screenshot Coverage

- Sandbox: 36 screenshots covering every required group page in dense and empty scenarios for desktop and mobile.
- CanDoItAll: 11 screenshots covering projects, validation, test lab, prompt factory, structure, and calendar desktop/mobile plus focused calendar crops.
- Zyphonote: 11 screenshots covering marketplace, playlists, events, learning builder, learning package, my scores, and seller profile on the required form factors.

## Resolved Proof Blockers

- Zyphonote seller profile had styling regressions after shared wrapper adoption because isolated CSS was not reaching shared component internals. The fix kept seller-specific styling in Zyphonote and restored layout with component-local and `::deep` styling.
- CanDoItAll project calendar wasted desktop width and broke down on narrow widths because the page layout reserved too much space beside the calendar and the week view always kept the mini-month rail. The fix stacked the page layout and made the timed week view responsive.
- Playwright proof screenshots intermittently blanked portions of live canvases. The proof harness now snapshots canvases into temporary PNG overlays before capture. This is proof tooling only and does not affect runtime behavior.
- The temporary runtime debug exposure used during diagnosis was removed before final signoff.

## Validation Questions

- Can I read all texts properly? Yes. The reviewed desktop and mobile surfaces remain legible, including the previously regressed seller profile fields and the calendar headers, time axis, and event chips.
- Will I like and understand this UI/layout as a new user? Yes. The reviewed pages now present a clear first-read structure with obvious primary content, predictable actions, and no broken or confusing dead zones.
- Is there any too large component, gap, or visual disruption? No blocker remains. The calendar width issue and seller-profile spacing issue were fixed, and the reviewed layouts no longer show oversized empty columns or collapsed form sections.
- Do we use proper components from shared libraries instead of custom ad-hoc markup? Yes. The seller profile now uses the shared input primitives that were intended for this migration, and the project calendar page consumes the shared calendar surface from CanDoItAll.
- Do we use available space properly? Yes. CanDoItAll calendar now uses the available width on desktop and mobile instead of starving the timed grid.
- Can the page be understood by scanning headings only? Yes. The required app pages preserve clear section headings and page-level structure without depending on decorative treatment.
- Does the hierarchy remain clear without decorative effects? Yes. Card boundaries, headings, field grouping, and summary sections still communicate the page hierarchy on their own.
- Do desktop and mobile layouts both feel intentional? Yes. The required mobile captures show deliberate single-column and compact states instead of desktop layouts merely squeezed into smaller widths.
- Are focus, hover, disabled, loading, and empty states coherent? Yes within the reviewed coverage. Sandbox dense and empty scenarios remained consistent, loading overlays clear correctly, and the shared form controls preserve predictable interaction styling.
- Did any app accidentally keep a dependency on old shared paths or styles? No accidental dependency was found on the reviewed routes. Shared calendar ownership remains in CanDoItAll, and seller-profile styling remains app-local in Zyphonote.
- Did any shared component regress into app-specific styling debt? No. The shared calendar fixes are generic responsive behavior changes, while seller-profile-specific styling was intentionally kept in Zyphonote rather than pushed into shared libraries.

## Residual Notes

- The only non-blocking build issue visible in the final proof wave is the existing `NU1510` warning pair in `CanDoItAll.Mcp.DotNetWatch`, duplicated once through solution-level restore and once through project build output. This did not affect bundle acceptance.
- No page errors or console errors were captured during the final screenshot run.
