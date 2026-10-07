# Why Workspace Settings Core is next

The current implementation includes the Resources Registry/Browse extraction after Memory.
RS-R1 is a bounded readiness correction, not a reason to halt all further extraction. [EV01, EV02]

Workspace is still a heavy Razor module referencing Infrastructure, Projects and Security;
its `/settings` route includes unrelated families of state and administration. [WS01, WS02, WS03]
Taking the whole module now would combine ordinary preferences, secret handling, database
switch/transfer, storage recovery and API account/token administration. The current tree
contains a 58,154-byte DatabaseSourcesSettingsPanel and a 33,031-byte StorageSettingsPanel,
plus a separate recovery dialog and API dialogs. File size is inventory, not a work estimate. [WS15]

## Selected complete feature surfaces

1. The real settings navigation/header and production route composition.
2. Workspace business defaults: all six fields, normalization, provider choice and save.
3. Secrets: metadata catalog/filter, explicit edit/reveal/copy, create/update/delete and outcomes.
4. Files: machine-local executable association overrides and return to system default.
5. Provider history: explicit load, future policy, bounded preview and confirmed shorter retention.

This is four local content sections plus their shared settings shell. The live navigation has
eight items: seven local tabs and the Providers redirect. Preserve their current order, tokens
and redirect behavior; do not call it an eight-section completed extraction. [WS01, WS02]

## Deliberately deferred implementations

`data-sources`, `storage` and `api-access` remain real production host-owned branches/slots.
Do not move their existing backend-bound child into the lightweight library. Render each only
when its route is active. Do not start extracting database transfers, storage placement
recovery, account administration or JWT token issuance in this task.

The standalone sandbox proves only the selected four sections and shell. Deferred destinations
may be labelled disabled navigation entries or an explicit out-of-scope notice, never a fake
working database/token/storage screen or claimed completion. A production smoke check must
still prove that their real existing components compose when selected. This explicit smaller
slice does not relax proof for any selected renderer or descendant.

## Follow-on ordering

After this slice, select a bounded Workspace API Access or Storage/database slice based on
its then-current coupling and owner risks; do not pre-authorize either here. Projects portfolio
and editor work remain candidates. Processes and Workbench remain late-stage modules as the
owner requested. This document is a sequencing recommendation, not a claim to have freshly
audited every remaining module or a fixed project-wide work queue.
