# UI parity protocol

## Baseline

At SB01, identify actual routes and capture a named large-screen desktop viewport, for example 1920×1080 or the repository's established maximized desktop proof size.

Capture:

- normal state;
- loading/empty/error state where reproducible;
- selected item;
- open switch/history/details dialogs;
- open floating catalog;
- active floating chat;
- relevant context/affinity controls;
- settings identity/runtime tabs;
- approval/execution state through a safe fixture when available.

Record visible copy, accessible names, `data-testid`, primary scroll owner, focus behavior, and overlay layering.

## Refactor checkpoints

At CP2 and CP3, reproduce only the affected high-risk baseline scenarios.

Inspect screenshots while visible:

- hierarchy and readability;
- clipping and overflow;
- first viewport;
- scroll ownership;
- spacing;
- disabled/busy/error states;
- focus;
- tooltip/menu/dropdown/dialog placement;
- overlay z-index and internal scrolling;
- visible actions.

Do not accept screenshots solely because files exist.

## Final pass

At SB09, run one focused end-to-end desktop pass across:

- Agent catalog/switch;
- thread selection/new thread/title;
- send and response;
- one approval/execution scenario;
- one attachment or prompt-gallery scenario;
- floating catalog/open/hide/reopen/stop;
- settings identity/provider/model/save;
- one contextual or Process consumer.

Voice may be an environment-dependent manual check, but the component and callback path still needs deterministic owner-test proof.

## Responsive scope

Preserve existing behavior outside the named desktop viewport. Do not add or tune mobile/tablet UI in this phase.
