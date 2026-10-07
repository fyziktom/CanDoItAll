# Source observations for PP1

These observations describe the pre-PP1 provider implementation at the reviewed ref. They are not newly attributed A2 regressions. Confirm concrete behavior with tests before changing it; preserve intended owner policy, not accidental state loss.

## PP1-01 · Same-target acquisition and refresh can replace the draft

`RefreshAsync` obtains catalog data and, for a saved selection, calls `SelectAsync(State.ProviderId)`. `SelectAsync` always advances selection, cancels its lifetime and creates a fresh EditContext after reading the editor; it has no acquired-same-target no-op. The same route is invoked by a provider tree click. A dirty saved-provider editor can therefore be silently replaced by a refresh or reselect. [R13, R14]

Specify the user transitions explicitly. A mere reselect of the successfully acquired same target preserves draft/context/raw values. Reference refresh must not become an implicit discard. Offer a deliberate discard/reload transition only if needed and make its consequences explicit. Failed/missing/unacquired same-ID retry must still load the exact target. A true target change/New retains its documented semantics and must not inherit another target's unsaved values. Do not fix this with an unconditional ID-only early return.

Test dirty text, raw invalid number, tags, suggested-model text and an open Thinking edit; refresh while a mutation or read-back is pending; A→B→A; deleted target; first-load failure; new unsaved draft; metadata failure with usable original editor. A false Ready state is not a successful acquisition.

## PP1-02 · Text mirrors and nested edits belong to the same lifetime

The host separately owns `providerTagValues` and `rawSuggestedModels`; `SyncProviderEditorText` is called after several awaits. Save pushes these mirrors into the live draft before operation admission. `ProviderEditorSubmission.Reconcile` can also change model arrays without updating those mirrors. This combination needs field/target-aware reconciliation, not a second writable model. [R13, R17]

Capture a complete submitted request before awaits. Preserve later raw input and the form context; adopt actual identity and confirmed concurrency metadata. Do not let a late metadata action or mirror synchronization overwrite successor input. Distinguish unchanged, later edited and changed-then-returned-to-the-same-text input where semantic choices need revisions. Cover post-commit name/model normalization, concurrent typing and save followed by another save. Never serialize mutable presentation objects as durable request history.

## PP1-03 · Form scope and validation

Four sections reuse the same form context, but server-rendered tab bodies and InputNumber widgets can be removed. Preserve unfinished numeric/JSON values and useful validation on tab changes; no broad disable-all strategy just to pass tests. Connection and runtime text currently use blur-based inputs; capture input before Enter/blur and actual immediate actions. [R12, R20, R21]

Do not introduce conflicting nested HTML forms for Thinking. Its Apply changes the provider draft; Save persists it. Price row indexes are not stable semantic identities. Tests must remove/reorder/rename rows while retaining other raw fields, and ensure stale callbacks cannot edit a replacement row.

## PP1-04 · Missing secret is not failed metadata acquisition

`HasUnavailableSecretReference` infers absence from a catalog that can legitimately have failed and returned no items. Its current explanatory text can claim a vault reference no longer exists when only metadata loading failed. Preserve the raw saved reference and distinguish unverified/unavailable metadata from confirmed missing reference. Do not resolve values to determine list labels. [R12, R13, R18]

## PP1-05 · Existing operation protocols are assets, not replacement targets

The operation owner distinguishes native writes, verified retry and discovery. A first save has a stable candidate ID; unknown results survive in `ProviderEditorRecovery`. Known commits bind identity before secondary reads. Health reads the saved provider and can persist diagnostic metadata; discovery starts from a copied draft and only applies model/price data locally. [R15–R17, R22, R24]

Characterize these protocols and preserve them. If the extraction reveals a precise publication/reconciliation bug, fix its narrow layer and test actual-owner behavior. Do not invent a new durable generic receipt registry or silently replay a diagnostic as a read-back.

## PP1-06 · Sharing and Thinking interaction

The local Thinking dialog uses current model/configuration and its captured row. A source-managed provider has read-only settings and a real source-refresh action. Separate local draft edits from publication, source synchronization and request history. Ensure an open row dialog cannot Apply against a replaced provider, changed kind/transport or a superseded configuration. Invalid JSON stays visible; no repair by replacing it with `{}`. [R19]

A source-owned display name is not its opaque model ID. Read-only controls are not backend authorization; keep both local UI guards and current native source-managed refusal. Merely opening the tab must not trigger health, discovery, model maintenance or remote synchronization.
