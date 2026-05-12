# Follow-up Request

Captured 2026-05-11.

## Raw User Additions

- Clean PostgreSQL database for this test and configure it as a datasource/profile usable when launching the app from Visual Studio.
- Add example processes/workflows with logic using IF/ELSE, SWITCH/default, and fan-out decisions.
- Improve decision blocks to look closer to the supplied workflow-canvas reference image: diamond-shaped IF/SWITCH/FAN-OUT nodes and intuitive branch connections.
- Add decision blocks to the canvas right-click second-layer menu and the toolbox.
- Improve the first setup dialog/floating window so it works well in maximized canvas and exposes meaningful setup fields for decision and other block types.
- Render setup-dialog content through a block-specific renderer seam so later plugins, especially executor blocks, can contribute their own renderer.
- Seed workflows for document summary, email processing variants, XLSX read/write with decisions, internet fetch into project structure, and at least 5-10 additional useful production examples.
- Tune LLM requests and workflow settings for production behavior.
- Execute/observe workflows, record problems, and repair the bundle or implementation based on observed trouble.

## Attached Visual Target

- `C:\Users\lucys\Downloads\Vygenerovaný obrázek 1.png`
- The image shows a workflow canvas with diamond decision nodes, labeled branch pills, colored branch connections, a right-side decision inspector, toolbox-style workflow shell, minimap, and readable non-overlapping branches.
