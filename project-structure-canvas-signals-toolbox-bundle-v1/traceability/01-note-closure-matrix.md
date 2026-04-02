# Raw Note Closure Matrix

| Raw note id | Exact wording | Normalized requirements | Impacted surface | Planned proof | Owning subbundle | Prerequisite signal | Exception |
|---|---|---|---|---|---|---|---|
| `N001` | `increase size of the markers glyph in second layer of right click menu` | `R001`, `C001` | Context-menu marker preset rendering | Browser screenshot + CSS inspection | `02-02-signals-toolbox-window-and-menu-polish` | After `01` only because the same marker catalog is reused | None |
| `N002` | `just assure that it will not increse size of that circle around glyph` | `R001`, `C001` | Context-menu badge sizing | Computed-size browser check | `02-02-signals-toolbox-window-and-menu-polish` | None | None |
| `N003` | `add new toolbox window for markers/progress and maybe few more things` | `R002`, `R004`, `R008` | Floating signals toolbox | Browser open-state proof | `02-02-signals-toolbox-window-and-menu-polish` | Depends on `01` | Interpreted `few more things` as priority plus clear/reset helpers |
| `N004` | `floating window over canvas (add button to show it to top canvas toolbar)` | `R002`, `C002` | Toolbar toggle and overlay window state | Browser toolbar interaction proof | `02-02-signals-toolbox-window-and-menu-polish` | Depends on `01` | None |
| `N005` | `I must select some node, and when I click on marker or priority icon, etc it adds it to node` | `R003`, `R004` | Selection-aware toolbox actions | Browser action proof on selected node | `02-02-signals-toolbox-window-and-menu-polish` | Depends on `01` | Multi-select support may be additive beyond the literal note |
| `N006` | `Assure we can add multiple markers to node` | `R005`, `R006`, `R007` | Marker persistence, projection, rendering | Focused tests + browser proof | `01-01-multi-marker-data-contract-and-rendering` | Critical foundation for `02` | None |
