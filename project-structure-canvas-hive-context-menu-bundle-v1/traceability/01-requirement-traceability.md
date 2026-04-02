# Requirement Traceability

## Raw Note Closure Matrix

| Raw note | Requirement mapping | Impacted surface | Planned proof | Owning subbundle |
| --- | --- | --- | --- | --- |
| `N001` | `RQ-01`, `RQ-06` | Runtime geometry and CSS sizing | Playwright open-menu screenshot review plus DOM geometry checks | `02-02-hive-geometry-and-submenu-packing`, `03-03-visual-polish-and-responsive-tuning` |
| `N002` | `RQ-06` | Visual composition boundaries | Browser screenshots and analytics review | `03-03-visual-polish-and-responsive-tuning`, `04-04-browser-proof-and-closure` |
| `N003` | `RQ-02`, `RQ-03` | Node action ordering | Focused component tests plus browser proof of first ring | `01-01-standard-ring-order-and-node-menu-contract`, `04-04-browser-proof-and-closure` |
| `N004` | `RQ-02`, `RQ-03` | Stable clockwise first-ring placement | Component tests and browser screenshot review | `01-01-standard-ring-order-and-node-menu-contract`, `04-04-browser-proof-and-closure` |
| `N005` | `RQ-03` | Cross-node consistency | Adapter and catalog tests plus representative-node browser pass | `01-01-standard-ring-order-and-node-menu-contract`, `04-04-browser-proof-and-closure` |
| `N006` | `RQ-04` | Overflow ordering and grouping | Component tests where practical plus browser screenshots | `01-01-standard-ring-order-and-node-menu-contract`, `03-03-visual-polish-and-responsive-tuning` |
| `N007` | `RQ-01`, `RQ-06`, `RQ-07` | Overall density, readability, and edge handling | Desktop and narrower screenshot review | `02-02-hive-geometry-and-submenu-packing`, `03-03-visual-polish-and-responsive-tuning` |
| `N008` | `RQ-05` | Keyboard and shortcut compatibility | Browser submenu-open path plus DOM checks for labels and focus | `02-02-hive-geometry-and-submenu-packing`, `04-04-browser-proof-and-closure` |

## Requirement To Bundle Mapping

| Requirement | Primary bundle location | Primary subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| `RQ-01` | `architecture/01-target-solution.md` | `02-02-hive-geometry-and-submenu-packing` | Browser screenshot and geometry review | `The request is fundamentally about composition, so browser proof is critical.` |
| `RQ-02` | `requirements/01-normalized-requirements.md` | `01-01-standard-ring-order-and-node-menu-contract` | Focused component tests | `The stable first ring must be explicit before geometry proof matters.` |
| `RQ-03` | `analysis/01-current-state.md` | `01-01-standard-ring-order-and-node-menu-contract` | Adapter and catalog assertions | `Cross-node consistency is the core product rule.` |
| `RQ-04` | `architecture/01-target-solution.md` | `01-01-standard-ring-order-and-node-menu-contract` | Component proof plus browser screenshots | `Overflow organization remains node-specific but must be intentional.` |
| `RQ-05` | `analysis/02-assumptions-and-risks.md` | `02-02-hive-geometry-and-submenu-packing` | Browser keyboard and submenu smoke | `The layout pass must not break the shortcut work shipped in the previous bundle.` |
| `RQ-06` | `inputs/02-structured-input.md` | `03-03-visual-polish-and-responsive-tuning` | Desktop and narrow screenshot review | `This is where space efficiency and visual coherence are judged.` |
| `RQ-07` | `plan/01-phase-plan.md` | `03-03-visual-polish-and-responsive-tuning` | Edge-safe browser proof | `Must validate near host bounds and on narrower widths.` |
| `RQ-08` | `reviews/01-execution-report.md` | `04-04-browser-proof-and-closure` | Execution report and validators | `Bundle closure requires analytics and raw-note closure, not just code.` |
