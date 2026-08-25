# UI architecture

## Scope

CanDoItAll application UI targets the supported large-screen desktop viewport. Do not spend
scope on a separate mobile composition. Reusable basic BaseLib components must continue to
obey their own size policy.

UI starts only after SB07.

## Ownership

Extend the current provider management surface under:

- `src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ProviderManagementPanel.razor`
- its code-behind and cohesive child components/view models.

The component calls Workspace application services. It does not:

- use EF directly;
- construct HTTP clients;
- resolve secrets;
- parse catalog JSON;
- mutate imported profile entities directly.

Avoid putting all new behavior into the existing code-behind. Extract cohesive child
components and presentation models.

## Desktop composition

### Locked compact-composition decisions

- **Primary surface:** the existing provider collection and selected-provider detail remain
  the dominant split work surface. Shared providers are ordinary rows with explicit origin and
  availability, not a separate decorative dashboard.
- **Supporting content:** source status, filters, eligibility explanations, and short
  diagnostics stay in the selected detail, compact section heads, badges, or dialogs. Long
  diagnostics and protocol detail are collapsed or secondary and must not push the provider
  collection below the first viewport.
- **Stats treatment:** statistics are supporting only. Use counts beside headings, badges, or
  `CompactStatStrip` when a count is useful. `SummaryTiles`, `StatsGrid`, and metric-card rows
  are `N/A` unless implementation evidence proves metrics became a primary task.
- **List/editor organization:** browsing/comparing providers remains list-first with the
  selected existing editor/detail visible beside or immediately within the established
  provider panel. Independent source create/edit uses a dialog; catalog discovery/import uses
  a separate dialog so it cannot permanently displace the provider list.
- **Text-area sizing:** no new prose text area is required by this feature, so semantic text-area
  sizing is `N/A`. If implementation discovers a genuine notes/diagnostic edit field, reopen
  this decision and use `TextAreaSize.Standard` for descriptions or `Extended` only for
  intentional long-form content.
- **Dialog sizing:** source create/edit uses `ModalSize.Medium`; catalog comparison and
  multi-select import uses `ModalSize.Wide`; retire/unpublish confirmations use
  `ModalSize.Compact`. Dialog bodies own overflow when needed while headers, validation, and
  footer actions remain visible.
- **First viewport:** at the named `1600x1000` viewport, provider identity, Local/Shared origin,
  availability/publication state, the primary list, selected-detail summary, and add/source/sync
  actions are reachable without page scrolling past repeated introductions or metric cards.
- **Scroll owner:** the established provider page/panel is the single vertical scroll owner.
  Only the catalog result region may own a bounded internal scroll inside its wide dialog; do
  not introduce competing nested page/list/detail scrollbars.
- **Compound controls:** tabs, filter/action rows, source actions, and dialog field groups must
  respond to their immediate container width. Prove them in the selected-detail column and in
  medium/wide dialog columns even though the viewport is wide.

Recommended information architecture:

### Provider list

- origin badge: Local or Shared;
- connector/purpose/model;
- enabled intent;
- health/availability;
- publication badge for central local profiles;
- source name for imports;
- actions appropriate to ownership.

### Local provider detail

- existing provider fields;
- Sharing section:
  - eligibility status and reasons;
  - publish/unpublish action;
  - public display/model/capability preview;
  - warning that upstream secrets remain central.

### Shared sources section/dialog

- source list;
- add/edit form;
- base URI;
- secret reference;
- trusted-network/TLS policy;
- enabled state;
- test connection;
- last sync/status/remote instance ID.

### Catalog import dialog

- source selector;
- refresh;
- provider/model/purpose/capability rows;
- multi-select;
- already imported/retired/unavailable state;
- confirm summary;
- no raw internal endpoint or secret information.

### Imported provider detail

Editable:

- local alias;
- local enabled intent;
- safe retire/remove action.

Read-only:

- source;
- remote display name;
- purpose;
- routing model/default model;
- capabilities;
- remote revision/availability;
- effective endpoint summary without credential.

## First viewport and scroll owner

SB08 must record the current provider panel viewport. Preferred:

- primary provider list and selected detail remain visible without nested competing scrollbars;
- the page/panel has one deliberate vertical scroll owner;
- catalog import uses a bounded dialog with its own list scroll only when necessary;
- primary add/source/sync actions are in the first viewport;
- long technical diagnostics are collapsed or secondary.

The final compact composition decision must be validated against the current
`candoitall-components-mcp` guidance.

Preparation loaded the current compact-composition reference. The Components MCP transport was
unavailable on two recommendation attempts (`Transport closed`); SB08 must retry the live MCP
before component selection and record either its recommendations or the explicit continued
tooling gap.

## Required states

- loading;
- empty local providers;
- no sources;
- source test success/failure;
- unauthorized/forbidden;
- unsupported schema;
- source identity mismatch;
- catalog empty;
- import in progress;
- idempotent already imported;
- stale/unpublished/missing;
- source offline;
- local profile disabled;
- publication ineligible with actionable reason;
- concurrency conflict;
- destructive action confirmation.

## Accessibility and safety

- labels and descriptions for status icons;
- keyboard-accessible dialog/list selection;
- focus restored after dialog close;
- no secret value rendered after save;
- no destructive default action;
- status not conveyed only by color;
- long IDs copyable but not dominant;
- error details sanitized.
