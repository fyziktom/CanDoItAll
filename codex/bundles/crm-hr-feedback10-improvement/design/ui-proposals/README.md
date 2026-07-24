# CRM-HR UI Proposals

These are design references, not pixel-perfect implementation contracts. The
implementation must preserve the existing CanDoItAll BaseLib/Tailwind visual
system and use the proposals to guide hierarchy, density, dialog boundaries,
and first-viewport behavior.

## Shared Direction

- Use case: `ui-mockup`.
- Target: maximized large-screen desktop, with `1600x1000` as the repository
  browser-proof baseline.
- Visual thesis: calm, dense, professional CRM operations with one dominant
  working surface, pale neutral surfaces, navy primary actions, thin borders,
  restrained shadows, and no decorative dashboard mosaic.
- Component thesis: existing BaseLib page, list/detail, tabs, filter, dialog,
  form, tag, badge, card/list, paging, and Charts primitives before custom
  structure or CSS.
- Interaction thesis: dialog-first independent create/edit flows, explicit
  selection and loading feedback, stable dialog footers, and restrained
  selection/step transitions.
- Constraints: large-screen application behavior only; no Radzen; no new
  gradients; no mobile redesign; no tiny text; no fabricated invoice data.

## Assets And Prompt Intent

| Asset | Prompt intent |
| --- | --- |
| `01-crm-workspace-large.png` | Redesign the full CRM workspace around a compact searchable account/opportunity collection, tabbed account details, compact supporting metrics, and a list-first Opportunities tab with a single `Add opportunity` action. |
| `02-add-contact-method-wizard.png` | Replace the failing inline contact row with a two-step `Add contact method` dialog: type-card selection, then validated contact details with TagEditor, notes, shareability, and a stable footer. |
| `03-party-record-picker.png` | Provide a reusable paged party picker for hundreds or thousands of people/organizations with search, typed scope, tags, selection summary, and server-style paging. |
| `04-create-opportunity-wizard.png` | Replace the stacked opportunity editor with a three-step `Create opportunity` dialog covering basics, ownership, and commercial context, including party/project picker triggers. |
| `05-opportunity-detail-dialog.png` | Open a selected opportunity in a read-only detail dialog first, with summary/stakeholder/activity tabs and an explicit `Edit opportunity` action. |
| `06-project-picker-dialog.png` | Reuse project-list vocabulary in a paged project picker with search, status, portfolio, tags, selection summary, and stable actions. |
| `07-financials-tab.png` | Add a task-first `Financials` tab with opportunity-derived sold/bought/net metrics, monthly/yearly grouped bars, sold-vs-bought doughnut distribution, and an explicit `Coming with invoicing` placeholder. |
| `08-opportunity-edit-dialog.png` | Keep opportunity editing isolated in a wide controlled dialog with explicit record selectors, commercial validation, stale-update context, and stable cancel/save actions. |

## Source References

- `../../inputs/feedback10-media/image1.png`: current contact editor and crash
  location.
- `../../inputs/feedback10-media/image2.png`: current stakeholder party
  dropdown.
- `../../inputs/feedback10-media/image3.png`: current opportunity filters and
  empty state.
- `../../inputs/feedback10-media/image4.png`: current account Overview surface.
- `../../inputs/feedback10-media/image5.png`: current full CRM workspace and
  duplicate shell-tab titles.

## Implementation Rules Derived From The Proposals

- The primary collection must be useful in the first viewport.
- The standard CRM page remains the intentional page scroll owner.
- Independent create/edit work belongs in controlled `Dialog` components with
  stable headers and footers.
- Selection dialogs own their internal result scrolling and keep paging and
  actions visible.
- Opportunity and project selection must not degrade to large native
  dropdowns.
- `Financials` derives current values from opportunity data; invoice status
  remains explicitly unavailable until invoicing exists.
