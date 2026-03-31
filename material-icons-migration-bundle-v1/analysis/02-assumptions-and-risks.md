# Assumptions And Risks

## Assumptions

- `CanDoItAll.Components.BaseLib` can safely host local static icon assets that flow through the web application via static web assets.
- A local copy of the Google Material Icons stylesheet and font can be checked into the solution without requiring runtime external requests.
- Most existing named tokens already resemble Material icon names closely enough that they can either be passed through directly or normalized by a small compatibility catalog.
- Canvas and Workbench shorthand badges are intentional UI affordances today, so they need explicit design decisions instead of silent replacement guesses.

## Critical Path Risks

- If the workbook census misses a renderer or raw glyph family, later route proof may look green while parts of the solution still render Font Awesome or text icons.
- If the local asset packaging or `Icon` renderer foundation is wrong, every downstream call site migration becomes hard to trust and may need reopening.
- If the shared component migration breaks button, tab, step, or treeview alignment, every downstream page proof is weakened because those components are reused broadly.
- If the dirty Workbench files are overwritten or merged carelessly, the icon migration could accidentally revert unrelated user work.

## Validation Risks

- Browser proof on `/projects/{ProjectId:guid}/structure` depends on having or creating a valid project fixture during execution.
- Font asset mistakes can compile successfully but still render missing glyphs, incorrect line-height, or clipping only at runtime.
- Workbench and Prompt Factory surfaces use many token-driven overlays and buttons, so broad icon regressions may only appear after real interaction rather than static inspection.
- If Playwright MCP is unavailable, the workflow still needs equivalent browser evidence and screenshot review rather than reasoning-only closure.

## Reopen Triggers

- Reopen subbundle `01` if execution discovers a new icon family, raw glyph escape, or token source that the workbook and CSVs did not capture.
- Reopen subbundle `02` if any runtime route still requests a remote icon asset or if shared icon rendering still emits Font Awesome classes.
- Reopen subbundle `03` if shared-shell or BaseLib components show icon misalignment, wrong affordances, or missing screen-reader labeling.
- Reopen subbundle `05` if Workbench or Prompt Factory proof exposes unresolved shorthand badges or collisions with the locally modified files.
