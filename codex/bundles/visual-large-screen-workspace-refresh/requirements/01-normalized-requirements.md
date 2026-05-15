# Normalized Requirements

| Id | Requirement | Acceptance signal | Owning subbundle |
|---|---|---|---|
| R-001 | Treat large PC screens as the only tuning target for this bundle. | Every proof plan uses a large desktop viewport first; no subbundle requires small or medium polish. | SB00-01, SB01, SB06 |
| R-002 | Adopt the Economy Simulator navigation concept: collapsed by default, icon-first, expandable, smooth, and low-copy. | App shell starts in collapsed mode on large desktop and can expand; navigation labels are compact; extra descriptions are in tooltips. | SB00-02, SB02 |
| R-003 | Move Settings and database switching from the top page area to bottom-left shell actions. | `MainLayoutTopBar` no longer renders database selector/state; Settings and DB switch are always reachable at bottom-left rail. | SB00-02, SB02 |
| R-004 | Increase usable workspace width across pages. | Shell/body surfaces use shared full-width/dense modes where appropriate; topbar chrome is reduced; right rail does not consume width when not required. | SB00-03, SB02, SB03, SB04, SB05 |
| R-005 | Create page inputs for every route/page group from real implementation. | `inputs/page-inputs` describes current elements, display, UX flows, tabs, dialogs, source files, target proposal, and function coverage. | SB00-01 |
| R-006 | Create `imagegen` design proposals for pages, tab contents, and dialogs. | Accepted proposal boards exist under `evidence/design-proposals/pages` and are mapped to every page input. | SB00-01, SB01 |
| R-007 | Confirm proposal coverage and regenerate inadequate proposals. | `analysis/03-imagegen-proposal-review.md` records coverage decisions and rejection/regeneration when a proposal violates a requirement. | SB00-01 |
| R-008 | Build generic reusable BaseLib/shared components first. | Foundation subbundles deliver shell/flyout/tree/detail/tab/dialog/metric/toolbar patterns before page-specific subbundles start. | SB00-02, SB00-03 |
| R-009 | Do not add page-local custom CSS for the refresh. | Implementation uses Tailwind classes, existing shared Tailwind sources, BaseLib component parameters/enums, or `Class` parameters; no new `.razor.css` files or ad hoc selectors for the refresh. | SB00-02, SB00-03, SB02, SB03, SB03-04, SB04, SB04-05, SB05, SB05-06 |
| R-010 | Use `TreeView` for projects, processes, workflows, and other large hierarchical lists. | Projects, processes, and workflows have tree/list-detail or tree/detail surfaces with stable selection, grouping, badges, and keyboard/focus proof. | SB00-03, SB03 |
| R-011 | Improve tab content and dialog content explicitly. | Process/live/workflow, plugins/prompts/settings, CRM/HR/operations tab/dialog states have implementation instructions and large-screen screenshot proof. | SB03-04, SB04-05, SB05-06 |
| R-012 | Move excessive secondary information into dialogs, flyouts, popovers, or detail panes. | Pages with crowded summary cards or long explanation blocks keep the main workspace clear while preserving reachable details. | SB00-03, SB03-04, SB04-05, SB05-06 |
| R-013 | Preserve architecture and strong typing while changing UI. | Tree adapters and shell state use typed models/enums/constants; no stringly-typed command identifiers beyond existing route strings or UI text. | SB00-02, SB00-03, SB02, SB03 |
| R-014 | Make the app suitable for a simple customer video presentation. | Large-screen screenshots show professional B2B hierarchy, readable density, clear navigation, and no obvious overlap/clipping. | SB04, SB04-05, SB05, SB05-06, SB06 |
| R-015 | Run a screenshot-driven repair loop against the reference design. | Execution report includes before/after screenshots, open-state overlay screenshots, route visual review answers, and repaired issues or explicit follow-up rows. | SB06 |

## Scope Boundaries

- No Radzen implementation: repo scan found no Radzen usage in `src` or tests.
- No mobile/tablet tuning.
- No marketing landing-page treatment; this is an operational B2B application surface.
- No generated image is proof of implementation quality. It only anchors design direction before browser validation.
- Generated image text is not source of truth; real labels and functions come from Razor/C# implementation and page inputs.
