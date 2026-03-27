# Target Solution

## Desired Design

- Keep the existing page/service/model split. Fix the issue in the workbench page and related view-model shaping rather than adding new layers.
- Treat the toolbox issue as a page layout and interaction problem first. The toolbox must render as an obvious accordion and remain unobstructed in the default desktop arrangement.
- Treat selection-panel cleanup as a descriptor-shaping problem. Node-specific facts, lead text, badges, and hints should be composed intentionally so the UI does not render duplicate information.
- Treat badge styling as a semantic mapping problem. File-type badges should come from one strongly typed profile path so the canvas and selection panel stay aligned.

## Boundaries

- UI rendering changes belong in the page Razor and CSS files.
- Non-trivial composition logic belongs in existing workbench model/descriptor helpers.
- File-type palette decisions should stay in the existing file visual-profile or graph-adapter model path.
- Tests should cover both state-shaping logic and page-visible behavior where practical.

## Expected Edit Shape

- Subbundle 01: adjust default floating-window layout and any related toolbox markup/CSS so accordion headers are visible and operable.
- Subbundle 02: prune selection facts and lead text by node type, adding contextual help affordances only where information remains necessary.
- Subbundle 03: remove file-type duplication in the selection panel and apply semantic badge tones with readable contrast.
