# Imagegen Proposal Review

## Review Standard

- Scope is large desktop only.
- Generated images are design planning evidence, not runtime proof.
- A proposal is acceptable only if it preserves the real implementation functions listed in `inputs/page-inputs`.
- If a proposal shows a required control in the wrong place or hides a real flow, the prompt must be tightened and the proposal regenerated before implementation planning relies on it.

## Proposal Assets

| Asset | Review result | Functional coverage | Visual/professional improvement |
|---|---|---|---|
| `pages/01-shell-baselib-corrected-proposal.png` | Accepted after regeneration. | Covers collapsed/expanded rail, right-side tooltip, bottom Settings/DB actions, DB flyout, safe copy, DB dialog entry, topbar without DB state, full-width workbench. | Strongest alignment with Economy reference: compact rail, less topbar clutter, clear workspace gain. |
| `pages/02-project-pages-tabs-dialogs-proposal.png` | Accepted. | Covers `/projects`, project wizard, hierarchy modal, Gantt preview, project structure canvas, project calendar/event detail. | Clear table/tree/detail composition, strong modal organization, better project video flow. |
| `pages/03-process-pages-tabs-dialogs-proposal.png` | Accepted. | Covers process library, Definition/Roles/Steps/Runs/Analytics/Exchange/Manager chat tabs, nested runs tabs, role/action/choose-run dialogs, live process dashboard and detail dialogs. | Closely matches Economy run observation density; tab bodies read as operational panels. |
| `pages/04-agent-workflow-tabs-dialogs-proposal.png` | Accepted. | Covers `/agents` tabs, AgentDetailsDialog tabs, chat/runtime/log dialogs, workflow dashboard/tree, workflow Editor/Templates/History/Analytics tabs, preview/run/event/editor dialogs. | Makes runtime/workflow complexity easier to scan and adds needed tree grouping. |
| `pages/05-core-pages-tabs-dialogs-proposal.png` | Accepted. | Covers dashboard, resources, plugins tabs/dialogs, prompt gallery, prompt factory support tabs/dialogs, settings and database transfer. | Replaces card-heavy admin pages with list/detail, tabbed workspaces, and compact dialog surfaces. |
| `pages/06-supporting-pages-tabs-dialogs-proposal.png` | Accepted. | Covers CRM/HR pages/dialogs, collaboration tabs, activity, automation, scheduler tabs/dialog, validation, and test lab. | Consistent B2B operational language and enough density for presentation recording. |
| `pages/07-baselib-reusable-components-proposal.png` | Accepted. | Covers reusable command rail, hover flyout, tree/detail workspace, dense tab workspace, inspector dialog, metric strip, toolbar, empty/loading/error states. | Establishes shared component direction before page work, reducing page-local styling risk. |

## Regeneration Log

- Initial shell proposal was rejected because it still implied a topbar database state and showed unsafe-looking database detail.
- Corrected prompt explicitly required no database selector or database chip in any topbar panel, DB/Settings only in the bottom rail, and masked database summary with safe-copy text.
- Corrected asset is `pages/01-shell-baselib-corrected-proposal.png` and is the only accepted shell proposal for implementation planning.

## Frontend-Skill Review

- The accepted boards follow app-UI guidance rather than landing-page guidance: no hero pages, no marketing panels, dense but readable workspaces, restrained accents, and primary work surfaces first.
- The boards use tree/list/detail layouts, compact metric strips, toolbars, dialogs, and tab bodies instead of dashboard-card mosaics.
- Cards are used mainly as bounded repeated items or dialog/panel surfaces. Implementation should keep this restraint and avoid nested card stacks.
- Text in generated images is pseudo-text and cannot be trusted for exact labels. Implementation must use the real route labels and function names from source.
- Mobile/medium proof is intentionally ignored per the architect's hard rule. Implementation still should not knowingly break shared components, but closure does not require mobile screenshots.

## Function Coverage Decisions

- Each page input maps to at least one accepted image panel.
- Every real tab listed by source scan has either a specific proposal panel or a grouped panel where the tab body is explicitly called out in the prompt and page input.
- Every real dialog family listed by source scan has a proposal panel or reusable `InspectorDialogScaffold` pattern.
- Implementation must verify coverage again with real screenshots. If a tab/dialog is materially different from its proposal, the owning subbundle must update its design instruction and capture a repair screenshot.

## Remaining Concerns To Enforce During Execution

- Generated proposals sometimes use generic business labels. Do not copy generated domain text into product code.
- Prompt Factory currently uses custom modal markup and page-local classes; this bundle should move touched dialog/tab chrome toward shared components instead of adding more page-local CSS.
- Some existing BaseLib components use large radii or card-like panels. The foundation subbundles must add denser enum variants rather than forcing pages into custom CSS.
- Database flyout copy must be intentionally safe and masked; never copy raw connection strings or credentials.
