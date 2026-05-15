# Structured Input

## Raw Notes

| Raw note | Exact or compressed wording | Normalized requirement ids | Owning subbundle |
|---|---|---|---|
| RN-001 | Improve visual look of the application; increase working space; make UI clearer. | R-001, R-004, R-012, R-014 | SB02, SB04, SB05, SB06 |
| RN-002 | Tune only for large screen; do not spend time on small or medium screens. | R-001 | SB00-01, SB01, SB06 |
| RN-003 | Use Economy Simulator concept: smooth UI, menu collapsed by default, opens when needed, collapsed items show tooltip on right side. | R-002, R-003, R-008 | SB00-02, SB02 |
| RN-004 | Reduce unnecessary menu text; keep items minimal and move extra info to tooltips. | R-002, R-003, R-008 | SB00-02, SB02 |
| RN-005 | Add Settings and Switch DB to bottom side of left menu; remove Switch DB from top page. | R-003 | SB00-02, SB02 |
| RN-006 | On mouseover of DB item show floating card with actual DB info and copy button. | R-003, R-012 | SB00-02, SB02, SB06 |
| RN-007 | App/page must use maximum available page width. | R-004 | SB00-03, SB02, SB03, SB04, SB05 |
| RN-008 | Analyze each page and get design proposals with imagegen; during repairs analyze screenshots and improve until similar. | R-005, R-006, R-007, R-015 | SB00-01, SB01, SB06 |
| RN-009 | Do not use own CSS; use Tailwind or BaseLib component improvements/enum options/Class parameters. | R-008, R-009, R-013 | SB00-02, SB00-03, SB02, SB03, SB04, SB05 |
| RN-010 | When a page has too much information, use dialogs to keep the page clear while preserving access. | R-011, R-012 | SB00-03, SB03-04, SB04-05, SB05-06 |
| RN-011 | Use treeview to show projects, processes, workflows, and other larger lists; follow Economy BU tree concept. | R-010 | SB00-03, SB03 |
| RN-012 | Customers need simple video presentation; app must look professional, understandable, B2B. | R-014, R-015 | SB01, SB04, SB05, SB06 |
| RN-013 | Create each page input describing real current elements, current display, and UX flows. | R-005 | SB00-01 |
| RN-014 | Generate proposals for pages, every tab content, and dialogs; confirm coverage and regenerate if insufficient. | R-006, R-007, R-011 | SB00-01, SB03-04, SB04-05, SB05-06 |
| RN-015 | Identify reusable generic BaseLib components from proposals and add their own starting subbundles. | R-008, R-009 | SB00-02, SB00-03 |
| RN-016 | After improving subbundles, revise proposals against frontend best practices and ensure subbundles cover all functions. | R-007, R-011, R-014, R-015 | SB00-01, SB06 |

## Large-Screen Rule

- Execution should use a large desktop viewport first, recommended `1920x1080` or larger.
- Small/medium layout cleanup is out of scope unless a large-screen change creates an obvious regression that blocks desktop use.
- Do not add a mobile validation loop to this bundle. Existing mobile behavior should not be intentionally broken, but it is not a completion gate.

## Visual Thesis

- CanDoItAll should read as a quiet, dense, professional workbench: compact icon rail, tree-first navigation, large full-width working surfaces, precise status signals, and progressive disclosure through tooltips, flyouts, and dialogs.

## Interaction Thesis

- Default navigation is collapsed and icon-first; hover/focus reveals concise right-side tooltips.
- Bottom-left shell controls always expose Settings and database switching without consuming topbar width.
- Dense pages shift from long card stacks to tree/list-detail workspaces, tabs, and dialogs for secondary information.

## Latest Repair Additions

- Page-input artifacts now describe current real implementation elements, display, and UX flows for route/page groups, tabs, and dialogs.
- `imagegen` proposals now cover shell/BaseLib foundations, project pages, process/live pages, agents/workflows, core admin pages, supporting pages, and reusable component candidates.
- Generic BaseLib component work is split into starting subbundles before page-specific changes.
- Tab/dialog-specific work has dedicated subbundles so broad density passes do not hide missing interaction states.
