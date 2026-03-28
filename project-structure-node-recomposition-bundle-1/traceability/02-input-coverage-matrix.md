# Input Coverage Matrix

| Raw note id | Exact wording | Normalized requirement ids | Impacted surface | Planned proof method | Owning subbundle | Exception status |
| --- | --- | --- | --- | --- | --- | --- |
| `N001` | `there must be new button in the toolbar, that will start automatic recomposition of the nodes across the available space.` | `R01`, `R07` | Project structure toolbar and workbench persistence | Component test, Playwright toolbar click, persisted reload check | `02` | None |
| `N002` | `I would not do it fully automated ... it makes sense to add it as button` | `R02`, `R08` | Page workflow and orchestration | Component test plus code review of entry points | `02` | None |
| `N003` | `user must select node reorganisation will be called for all nodes bellow it` | `R03`, `R07` | Selection model, subtree traversal, persistence | Service or unit test on selected-root descendant set | `01` | None |
| `N004` | `reorganisation does not mean reconnection of the nodes. It is just their positioning` | `R04` | Layout engine and service persistence | Integration test that links and parents remain unchanged | `01` | None |
| `N005` | `mindmap goes in practically just one direction ... It should go more in the circle` | `R05` | Layout algorithm and browser-visible composition | Playwright screenshot review and canvas-overlap evaluation | `02`, `03` | None |
| `N006` | `system must be sure there are no colisions of the nodes on the canvas.` | `R06` | Collision detection and browser proof | Automated rectangle-overlap assertions plus real browser check | `01`, `03` | None |
| `N007` | `analyze in preparation stage possible approaches and known algorithms` | `R09` | Bundle analysis and architecture | Bundle review of current-state analysis and target-solution file | `01` | None |
| `N008` | `prepare and execute it until it is fully done` | `R10` | Bundle workflow and closure | Validator passes, execution report, browser analytics | `03` | None |
| `N009` | `there is lots of not used space around the root node` plus the screenshot | `R05`, `R06` | Browser-visible canvas composition | Screenshot comparison and visual review questions | `02`, `03` | None |
| `N010` | `if I recompose from root, it must go in layers ... first layer it must do in clockwise and place each first layer node on proper "hour"` | `R11` | Root-level branch partitioning and first-ring ordering | Engine or integration assertions around first-layer angular ordering | `01` | None |
| `N011` | `Then seconds and other layers are going in the way of "hour" of that first layer node.` | `R12` | Deeper-layer branch steering | Integration or engine assertions that descendants stay in their branch sector | `01` | None |
| `N012` | `we still must prefer proper balance with distances ... putting nodes too close to each other reduce readibility.` | `R13` | Branch spacing and readability | Browser proof on the large validation project plus spacing-sensitive assertions | `01`, `02`, `03` | None |
| `N013` | `Like imagine invisible buble around them and those bubble cannot has colision.` | `R12`, `R14` | Branch-group bubble separation | Automated branch-bubble collision proof plus browser review | `01`, `03` | None |
| `N014` | `Project "project-structure-mcp-validation-1 workbench" is large and good for those testings.` | `R15` | Browser validation target | Real Playwright proof on the named project route | `02`, `03` | None |
