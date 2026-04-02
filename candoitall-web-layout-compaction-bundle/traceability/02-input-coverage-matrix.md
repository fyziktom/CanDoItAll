# Input Coverage Matrix

| Artifact / Note | Normalized Requirements | Impacted Surface | Owning Subbundle | Planned Proof | Exception Status |
| --- | --- | --- | --- | --- | --- |
| `ART-01` large-screen-first optimization | `R-01`, `R-04`, `R-09`, `R-10` | Shell, all main routes, final browser audit | `01`-`05` | Large-screen screenshots and execution analytics | `None` |
| `ART-02` projects screenshot showing stacked filters and wasted space | `R-01`, `R-02`, `R-03`, `R-05` | `/projects`, project board, shell top bar | `02` | `/projects` desktop screenshot plus open-state modal proof | `None` |
| Request note: search, selects, and reset should share one large-screen row | `R-02`, `R-06` | Projects board filter toolbar | `02` | `/projects` DOM snapshot and screenshot | `None` |
| Request note: analyze other pages and make them more compact | `R-04`, `R-05` | Dashboard, operational list/detail pages, settings, prompt factory, workbench | `03`, `04`, `05` | Route-by-route analytics rows | `None` |
| Request note: helper text may move behind a tiny blue `?` tooltip | `R-03` | Page headers, board intros, modal helper copy | `01`, `02`, `03`, `04` | Open-state help affordance proof | `None` |
| Request note: create subbundles and checklists | `R-10` | Bundle structure | Bundle-wide | Validator pass and README audit | `None` |
| Request note: tune components when they are not flexible enough | `R-06` | Shared form, toolbar, dialog, and layout primitives | `01` | Shared component diff plus route-level checks | `None` |
| Request note: prefer Tailwind or classes over pure CSS | `R-07` | Styling implementation path | `01`-`04` | Diff review | `None` |
| Request note: assure Tailwind watch is running and imported-file changes propagate | `R-08` | Tailwind build pipeline | `01` | Watch log and rebuilt stylesheet | `None` |
