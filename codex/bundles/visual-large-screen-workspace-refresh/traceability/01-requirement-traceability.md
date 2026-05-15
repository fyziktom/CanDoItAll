# Requirement Traceability

## Requirement Matrix

| Requirement | Raw notes | Bundle destinations | Owning subbundles | Proof method |
|---|---|---|---|---|
| R-001 large-screen-only target | RN-002 | `inputs/02-structured-input.md`, `plan/01-phase-plan.md`, all subbundle browser logging sections | SB00-01, SB01, SB06 | Execution report viewport rows show large desktop only and no small/medium tuning gate. |
| R-002 collapsed Economy-style navigation | RN-003, RN-004 | `architecture/01-target-solution.md`, SB00-02, SB02 | SB00-02, SB02 | Shell screenshots: collapsed, expanded, active item, tooltip. |
| R-003 bottom Settings/DB controls | RN-005, RN-006 | SB00-02, SB02 | SB00-02, SB02 | Topbar screenshot lacks DB switch; bottom controls and DB flyout visible; safe copy verified. |
| R-004 maximum workspace width | RN-001, RN-007 | SB00-03, SB02, SB03, SB04, SB05 | SB00-03, SB02, SB03, SB04, SB05, SB06 | Route screenshots reviewed for wasted gutters and shell/sidebar width. |
| R-005 page inputs from real implementation | Latest request | `inputs/page-inputs/*.md` | SB00-01 | Page inputs list current elements, display, UX flows, tabs, dialogs, and source files. |
| R-006 imagegen proposals for pages/tabs/dialogs | RN-008, latest request | `evidence/design-proposals/pages`, `analysis/03-imagegen-proposal-review.md` | SB00-01, SB01 | Accepted proposal assets mapped to every page input; rejected shell v1 documented. |
| R-007 proposal coverage confirmation and regeneration | Latest request | `analysis/03-imagegen-proposal-review.md` | SB00-01 | Review rows confirm functional coverage and regeneration when a proposal violates a hard rule. |
| R-008 reusable BaseLib components first | RN-009, RN-011, latest request | `inventories/02-reusable-baselib-component-candidates.md`, SB00-02, SB00-03 | SB00-02, SB00-03 | Component tests/examples and diff review show shared primitives before page wiring. |
| R-009 no new custom CSS | RN-009 | `architecture/01-target-solution.md`, all implementation subbundles | SB00-02, SB00-03, SB02, SB03, SB03-04, SB04, SB04-05, SB05, SB05-06 | Diff review confirms no new `.razor.css` or one-off CSS selectors for refresh. |
| R-010 treeviews for projects/processes/workflows | RN-011 | SB00-03, SB03 | SB00-03, SB03 | TreeView DOM/screenshot proof and typed adapter tests. |
| R-011 tab and dialog content improvements | Latest request, RN-010 | page inputs, SB03-04, SB04-05, SB05-06 | SB03-04, SB04-05, SB05-06 | Screenshots for every changed tab body and open dialog state. |
| R-012 dialogs for too much information | RN-010 | SB00-03, SB03-04, SB04-05, SB05-06 | SB00-03, SB03-04, SB04-05, SB05-06 | Dialog open-state screenshots and page review rows. |
| R-013 architecture and strong typing | RN-009, RN-011 | `architecture/01-target-solution.md`, SB00-02, SB00-03, SB03 | SB00-02, SB00-03, SB03 | Unit/component tests for typed rail/tree builders and state. |
| R-014 professional B2B video-readiness | RN-001, RN-012 | proposals, SB04, SB05, SB06 | SB04, SB04-05, SB05, SB05-06, SB06 | Final screenshot review answers hierarchy, clarity, density, no clipping. |
| R-015 screenshot-driven repair loop | RN-008, RN-012 | SB06, `reviews/01-execution-report.md` | SB06 | Browser analytics and raw-note closure rows populated. |

## Raw Note Closure Plan

| Raw note | Planned status at closure | Owning subbundles | Required proof |
|---|---|---|---|
| RN-001 | Solved or Partially solved with route exceptions | SB00-03, SB02, SB03, SB04, SB05, SB06 | Before/after route screenshots and visual review answers. |
| RN-002 | Solved | SB00-01, SB01, SB06 | Large-screen proof rows; no small/medium gate added. |
| RN-003 | Solved | SB00-02, SB02 | Collapsed/expanded shell and tooltip screenshots. |
| RN-004 | Solved | SB00-02, SB02 | Sidebar label and tooltip proof. |
| RN-005 | Solved | SB00-02, SB02 | Topbar removal plus bottom action proof. |
| RN-006 | Solved | SB00-02, SB02, SB06 | DB flyout open-state screenshot and safe copy assertion. |
| RN-007 | Solved or Partially solved with route exceptions | SB00-03, SB02, SB03, SB04, SB05 | Full-width screenshots and explicit exceptions. |
| RN-008 | Solved | SB00-01, SB01, SB03-04, SB04-05, SB05-06, SB06 | Page-input inventory, proposal review, and repair-loop screenshots. |
| RN-009 | Solved | SB00-02, SB00-03, SB02, SB03, SB03-04, SB04, SB04-05, SB05, SB05-06 | Diff review and component/Tailwind-only evidence. |
| RN-010 | Solved or Partially solved with page exceptions | SB00-03, SB03-04, SB04-05, SB05-06 | Dialog/flyout proof for dense pages. |
| RN-011 | Solved or Partially solved with justified exceptions | SB00-03, SB03 | Projects/processes/workflows TreeView proof. |
| RN-012 | Solved | SB06 | Final large-screen visual review against B2B/video criteria. |
| Latest page-input/proposal request | Solved | SB00-01, SB00-02, SB00-03, SB03-04, SB04-05, SB05-06 | Page input files, accepted proposal assets, component candidate inventory, and updated subbundle plan. |
