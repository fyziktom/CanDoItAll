# S0 causal follow-ups and first-stage gate

Read [model parity](MODEL_PARITY_CONTRACT.md) and the real [fixture runbook](MULTI_INSTANCE_RUNBOOK.md).
A passing echo fixture does not close either source-derived issue below.

## S0-R1 — model tooltip uses a routing ID as a name

R04 `ProviderProfilePresentation.BuildTreeTooltip` uses raw `provider.DefaultModel`; R05 binds
that text to actual provider tree nodes. Create an imported profile whose default route is
`sp1...` and whose ModelCatalog provides an independently different display label. Compare
Connection, Runtime, Thinking, Prices, the actual tooltip/title and accessibility text.
The human-facing name must use the proper mapping. Internal node keys, DOM option values,
persisted agent selection and inference request model MUST retain the opaque ID.

Test a local profile, an imported profile, a renamed source/provider, unavailable metadata and
multiple providers with the same visible model name. No blanket string replacement of `sp1`.
No model display label is an authority or stable uniqueness key.

## S0-R2 — imported snapshot mixes old model values with new labels

Read R24 RefreshAsync/AcquireAsync and R03 suggestedModelsText/ProviderDefaultModelText.
The acquired same-target no-op is correct for local drafts, but all immutable remote fields
must reflect one coherent accepted imported snapshot.

Reproduction with real component services:
1. Load imported P at revision V0 with default A and models A,B. Capture Context and current IDs.
2. Have a separate owner/circuit synchronize a valid V1 with default C and models B,C. Keep P's
   local ID unchanged. This must be an actual accepted source update, not a mutation of the old
   UI object in place.
3. Invoke the existing toolbar Refresh, without changing selection or forcing a page reload.
4. Observe which catalog and draft fields are used. Assert the complete expected V1 model names,
   default, capabilities/prices/Thinking and read-only status rather than only heading/count.
5. Repeat through two browser contexts against the same client application, then across central
   and independent clients. Record both publication and local import revisions.

Repair options are deliberately not prescribed as a class hierarchy. Introduce an exact
remote-owned snapshot adoption/reacquisition rule using existing import/provider revisions;
keep writable local draft retention and original mutation receipts. A plain Refresh must not
repeat source synchronization or perform a new mutation. Loading/error must remain explicit.

Required negative/control cases:
- LOCAL dirty provider, raw invalid JSON/price text and row context survive metadata refresh.
- unchanged imported snapshot/reselect is a no-op; avoid repeated database reads per render.
- initial or failed same-ID acquisition can retry; missing provider never becomes fake Ready.
- local imported alias/enabled edits in Sharing are owned separately and survive remote-only
  metadata updates unless an explicit conflict requires review; do not remount them blindly.
- V0 late success/error cannot overwrite V1; A→B→A; source disabled/retired/missing; partial
  metadata failure; already-issued source delivery cannot overwrite a newer local selection.
- saved agent points at removed model: preserve selection as unavailable, do not choose new default
  or nearest name. Reenable only through existing explicit operator/native transitions.

The review did not reproduce runtime effects. Confirm with failing-first tests before changing
behavior. If a fuller runtime path supplies a missing fence, document that evidence and close
as NOT_REPRODUCED with the exact controlling tests rather than creating redundant code.

## Stage S0 acceptance

Core model-name/default/routing/credential/no-fallback scenarios must pass on actual containers
before structural PP2 changes begin. If another critical problem is found, localize the owner,
known write stage, actual effect and consumer impact. Repair bounded causes; map complex protocol
changes separately. Preserve failed tests and native artifacts in private owned storage.
