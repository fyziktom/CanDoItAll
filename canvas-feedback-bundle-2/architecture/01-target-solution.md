# Target Solution

Use the shared workbench/canvas layers as the source of truth:

- fix help overlay positioning in shared canvas CSS so every consumer gets the centered behavior
- enable markdown upload by adjusting the typed create definition that drives the shared create composer
- strengthen file-node backgrounds through the existing palette system rather than subtype-specific page CSS
- move preview-style dialogs into the `CanvasWorkbench` overlay slot so they live in the same visual shell as floating windows and other canvas overlays

Important boundary:

- the page should not bypass the shared canvas overlay model just to solve PDF preview layering
