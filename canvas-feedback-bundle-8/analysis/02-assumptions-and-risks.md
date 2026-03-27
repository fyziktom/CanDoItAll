# Assumptions And Risks

## Assumptions

- The requested accordion behavior means the default desktop layout must make the toolbox directly operable without asking the user to dismiss another floating window first.
- Search is already acceptable, so the fix should preserve automatic expansion of matching groups while still presenting a Visual Studio-like grouped toolbox.
- Help hints are only needed where removing text would otherwise hide important guidance.
- Badge colors should follow the meaning of the file type rather than a generic accent-only badge row.

## Risks

- Moving or resizing floating windows can have side effects on unrelated workbench workflows.
- Aggressive pruning of selection content can remove useful context from uncommon node types.
- Tooltip help can become noisy if added broadly instead of only where the removed text carried real meaning.
- Semantic badge colors can become inconsistent if the style mapping is spread across multiple code paths.

## Mitigations

- Scope layout changes to the project-structure page toolbox and nearby floating windows only.
- Audit the node-type descriptor and selection-record pipeline before removing text.
- Centralize badge semantic decisions in the existing file visual-profile path if possible.
- Require Playwright screenshots after each subbundle to catch layout or contrast regressions immediately.
