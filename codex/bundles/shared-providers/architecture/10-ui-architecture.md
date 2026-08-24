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
