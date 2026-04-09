# Assumptions And Risks

## Assumptions

- The requested Playwright proof can be satisfied in this thread through the available Playwright CLI skill plus screenshot artifacts because no dedicated Playwright MCP action surface is exposed here.
- The dedicated sandbox route can be added inside `CanDoItAll.Components.Sandbox` without needing a separate app beyond that sandbox project itself.
- Existing `TabsVariant`, `TabPosition`, and render-mode semantics should remain compatible unless an explicit bug fix requires a narrowly documented adjustment.
- Root-level customization should follow the repo’s normal `StyledComponentBase` pattern unless another existing BaseLib convention proves more appropriate during implementation.
- The optional border request applies to tab buttons, not only to the content panel.

## Critical Path Risks

- If subbundle 01 removes `zy-*` classes before a CAD/CDA Tailwind replacement is complete, downstream screenshots may look worse while still working structurally, which would invalidate later proof.
- If the dedicated sandbox examples are added before the shared tabs contract is stable, the examples could hard-code around defects and hide component-level issues instead of revealing them.
- If wrap, scroll, or truncation behavior is not made intentional, the edge-case examples may pass on desktop but fail on narrow widths, forcing rework after screenshots already exist.

## Validation Risks

- Tailwind-source edits require rebuilding `output.css`; stale generated CSS could make the browser state look inconsistent with the source diff.
- The sandbox project may require a dedicated managed watch session rather than the default web app, so route proof depends on starting the correct project path.
- Terminal Playwright proof requires `npx`; that prerequisite must be checked before browser automation is attempted.
- Large-screen screenshots alone are insufficient for the requested edge cases, because narrow-width wrapping behavior is part of scope.

## Reopen Triggers

- Reopen subbundle 01 if the rendered component still emits shared `zy-*` selectors after the intended unification.
- Reopen subbundle 01 if the dedicated sandbox examples expose clipping, unreadable focus state, weak active-state contrast, or class-extension gaps that originate in the shared component rather than the sandbox page.
- Reopen subbundle 02 if the dedicated tabs page relies on page-local structural CSS to hide a shared component defect.
- Reopen subbundle 03 if browser proof is missing a real headed browser pass, explicit screenshot review answers, or narrower-width validation.
