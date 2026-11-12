# Resource R1 native cutover readiness

Source review only, 2026-10-17. No production or test edits, execution, database access, or model changes are represented here. The immutable [owner readiness note](../plan/P05-R1-resource-implementation-readiness.md) (`891F240AF673C80A5717DFBFD9DB2AA1DE60A1A148A7B6C9D281232702EA85BC`) remains the owner contract proposal; actual contract source must be frozen before native authoring.

## Ownership and dependencies

Persistence owns Resources.Contracts, owner preparation/read/write/promotion, Search integration, entity configuration, and backend proof. This slice owns Resources.Presentation, Resources.UI, the existing Resources sandbox, native component tests, and the existing Resources browser tests. Parent owns migration generation and all execution. No new project or package is needed. Historical, C3, performance, and SPA inputs remain separate.

The native boundary needs the reviewed immutable context, expectation, values, snapshot and outcome types. `ResourceMetadataValues.Capture` must validate the raw editor and configuration before defensive copying; calling the current unbounded `ResourceEditorModel.Capture` first defeats that boundary. New registry drafts and promotion drafts capture the dataset context when opened. A first Save cannot silently attach an older draft to a restored dataset.

## Minimal lifetime changes

1. Keep one `ResourceRegistryDraft` and `EditContext` through project and connector field edits. Keep its original/current accepted expectation separate from editable target project and connector values. Genuine New, navigation, failed exact acquisition and disposal retain their existing lifetime fences. A project edit must not reset the draft identity; a connector edit must not replace the entire draft.
2. Add project/connector edit versions and a configuration generation to the existing draft. Explicit connector/schema selection may replace its `ConfigurationInputDraft` and prune incompatible fields, while preserving scalar edit versions and the original expectation. This prevents removed raw fields from resurfacing and prevents an earlier save from reconciling into a new configuration generation. The shared Configuration UI helper needs no change.
3. Freeze one immutable command and the field/version witnesses before dispatch. Retain the returned accepted snapshot in the receipt and advance the same draft's accepted expectation before local `CreateEditor(snapshot)`, route publication, notifications or list refresh. A projection failure is a presentation failure after acceptance. It must not replace a known accepted result with Refused/Unknown or discard its exact revision.
4. Reconcile from that exact accepted snapshot, with no post-save `GetAsync`. Apply each unchanged scalar only when its value and edit version still match submission. Apply configuration, exact stored `ConfigJson` and `LocationOrIdentifier` only while the submitted connector/schema/configuration proposal still matches; the accepted base remains exact even when visible post-dispatch edits differ. A different selected draft is never rebased by late completion.
5. Use separate typed save/delete affordances. A bounded metadata-only read can retain an exact delete expectation and render an explicit read-only state without decoding configuration or inventing a current project. A corrupt binding has no actionable expectation. Existing unavailable connector and governed-object states remain visible; the stored governed disposition must not be escaped by changing the proposed connector.
6. Pending and Unknown block the original context/resource identity, including a preallocated create candidate, across reselection within the controller. A current read records only a separate observation. It never changes Unknown to Reviewed, proves rollback, changes the expectation or silently authorizes another write. Explicit New creates another draft; it does not clear the original evidence.
7. Promotion captures the original catalog context and candidate ID with the selected file occurrence. Retain Created/Reused exact snapshots before registry refresh or source reopening. Reused does not acknowledge the newly requested name/sensitivity. Later callbacks throwing even a refusal-shaped exception cannot erase a returned acceptance. Current read/reopen after an unknown promotion keeps the original hold.

## Existing proof that changes meaning

The lightweight `tests/Components/CanDoItAll.Resources.UI.Tests` sources contain **71 rows**: 45 Facts and 26 InlineData rows across 11 Theories, with no MemberData. This is source accounting, not discovery or execution. Per file: Boundary 3, Browse 23, ContractCompatibility 2, EditorReadiness 10, Registry 20, PageLoadGeneration 2, SurfaceTab 1, Surface 10.

Existing arrangements to convert deliberately:

- `Project_or_new_draft_lifetime_cannot_adopt_an_old_create`: distinguish a proposal's project edit from an actual replacement draft. The former keeps the exact accepted base and later project proposal; the latter remains retired.
- `Confirmed_new_id_survives_a_missing_postcommit_read...`, `Newer_save_readback...`, and `Older_save_refresh...`: replace obsolete post-save-read barriers with exact acceptance plus later external edit/projection or reference-refresh barriers. Assert no acknowledgement reread.
- `Unknown_review_is_single_flight...` and the existing sandbox browser's `Reviewed` assertion: require Unknown and its dispatch hold to remain after successful current observation, including reselection of the same identity.
- Extend existing post-dispatch raw-input, edit-away/back, disposal, promotion callback, and reference-read arrangements; preserve their real gates and no-write assertions.

New proof obligations are exact accepted revision/base advancement after target/config edits; accepted snapshot retention when local projection fails; context replacement between draft opening and Save; metadata-only unbound/retired deletion; stored-governed refusal; and promotion Created/Reused exact snapshot semantics. Final new row counts belong to the authored source inventory, not this proposal.

The owned compatibility conversions also include `ResourcesExactEditorHostTests`, `ResourceFileBrowsePaneTests`, Resource setups in `OwnerPostcommitPageTests` and `ResourcesPageUrlStateTests`, and the two existing `ResourcesBrowserTests` Facts. Keep the actual host/SQL proof distinct from the sandbox's typed outcome simulation. Use the existing 1600×1000 sandbox browser and native fixture; no new host framework or automatic retry.

## Rendering and release boundary

Retain the current list/detail editor and Browse dialog, history placement, and scroll ownership. Reuse the existing Alert, TextBlock, Button and action-footer patterns. Add only the metadata-only read state and truthful outcome/action state required by the owner cutover. Update only affected Resource rendering-register rows. The Components skill was read; its MCP component tools are unavailable in this session, so any new component selection needs a fresh capability check or direct shared-component source inspection before markup authoring.

Freeze exact native predecessor/successor hashes after backend signatures settle. Parent then owns standard builds, source-counted focused discovery/execution, browser inspection, and the final portability/static gate. No capacity, retained-data compatibility, durable Resource replay or navigation-safe unknown-recovery claim follows from this native slice.
