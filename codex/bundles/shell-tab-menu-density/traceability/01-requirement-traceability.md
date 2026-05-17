# Requirement Traceability

| Requirement | Source input | Owning subbundle | Proof |
| --- | --- | --- | --- |
| `R001` | `N001`, `N002` | `01-01-tab-header-density` | Component markup check, large desktop browser screenshot |
| `R002` | `N001`, `N002` | `01-01-tab-header-density` | Narrower viewport browser check |
| `R003` | `N003` | `02-02-sidebar-overflow-continuation-menu` | CSS review, component rendering test, large desktop screenshot |
| `R004` | `N004` | `02-02-sidebar-overflow-continuation-menu` | Component rendering test and open-state browser proof |
| `R005` | `N005` | `02-02-sidebar-overflow-continuation-menu` | Open-state screenshot and CSS grid review |
| `R006` | `N003`, `N004`, `N005` | `02-02-sidebar-overflow-continuation-menu`, `03-03-validation-and-closure` | Targeted tests and browser route checks |

## Raw Note Closure Matrix

| Raw note | Exact wording summary | Normalized requirements | Owning subbundle | Planned proof |
| --- | --- | --- | --- | --- |
| `N001` | Move tab search bar to same row as tabs at the end | `R001`, `R002` | `01-01-tab-header-density` | Browser screenshot plus markup/CSS evidence |
| `N002` | Move tab stat/status badges to same row as tabs | `R001`, `R002` | `01-01-tab-header-density` | Browser screenshot plus markup/CSS evidence |
| `N003` | Fix page height limitation and remove menu internal scrolling | `R003` | `02-02-sidebar-overflow-continuation-menu` | CSS proof and browser screenshot |
| `N004` | Overflow becomes final standard `more_up` item opening on mouseover | `R004`, `R006` | `02-02-sidebar-overflow-continuation-menu` | Component test and hover/focus browser proof |
| `N005` | Overflow pages are small square icon cards, max three rows, dark background | `R005` | `02-02-sidebar-overflow-continuation-menu` | Screenshot review and CSS grid proof |
