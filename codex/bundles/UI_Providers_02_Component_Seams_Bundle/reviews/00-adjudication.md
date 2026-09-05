# Independent pre-implementation adjudication
Observed clean local/remote HEAD: 7684f25854594f4a4b5486559890164aec382fb7. Evidence is current source and existing tests, not the external SHA assertion. All implementation changes below remain pending at this entry review.

| # | Finding and verdict | Current evidence / required correction |
|---|---|---|
| 1 | Confirmed, intentional Providers-01 boundary | ProviderProfilesSession/Reads owns reads; AgentProviderProfilesPanel still invokes Save/Delete/Health/pricing and shared LoadAsync. Move effect orchestration to an independently testable target owner. |
| 2 | Confirmed | Registry Save commits then observers/projection. Panel catches generically before binding returned ID. Existing projection exception already proves identity; observer failures lack that contract. Bind known identity before reads; never replay write for reconciliation. |
| 3 | Confirmed with transaction nuance | SecretMutationScope may own a serializable transaction; with no secret scope SaveChanges is canonical commit. With a relational scope CommitAsync is the boundary. Dispose, observers and projection follow. Cancellation during persistence cannot automatically prove rollback. |
| 4 | Confirmed | Panel passes mutable providerModel after modifying raw text/tags. Capture independent immutable submission synchronously and preserve edits made later. |
| 5 | Confirmed | Both child EventCallback ProvidersChanged maps to parent LoadAsync -> RefreshAsync -> selected SelectAsync. This replaces existing editable local drafts. Separate catalog and selected projection reconciliation. |
| 6 | Confirmed | SourceOperationResult has affected/retired lists; UI only uses counts then untyped callback. Carry application-produced scope. |
| 7 | Confirmed | Sharing panel loadedProviderProfileId has no generation, token or disposal. Old profileState can overwrite B and supply A to a command. Target-scoped owner required. |
| 8 | Confirmed | Sources overlay has no cancellation/disposal, and global isBusy/finally/notifications outlive close. Own overlay/operation lifetime and preserve backend commit classification independently. |
| 9 | Confirmed | Source updates/enabling/test failures change materialized state without parent callback. Test success affects source status as well as mismatch. Emit typed affected scope for every actual committed change. |
| 10 | Confirmed | SourceService Update/SetEnabled computes affected IDs then returns only source ID/token. Extend result, including warning and commit scope. |
| 11 | Confirmed | Import SaveChanges, source/reconciliation transaction commits and publication SaveChanges precede observers/activity. These failures are secondary; preserve receipts/warnings through later read failures/cancellation. |
| 12 | Confirmed protection; refutes any UI-only interpretation | Registry generic Save checks current and requested shared-import connector; registered deletion guard checks import/publication references. Preserve backend enforcement and audit rows. |
| 13 | Confirmed | RuntimeAdministration.CreateOrUpdateProviderModelAsync invokes diagnostics before registry Update rejects. Diagnostics can invoke model maintenance externally. Reject source-managed maintenance before diagnostics; direct zero-call proof. |
| 14 | Confirmed and preserved | Source-managed health uses sanitized result without generic registry persistence; test chat sanitizes boundary exceptions and checks availability. Ownership restriction must not block these allowed operations. |
| 15 | Confirmed accidental read-side lifecycle | GetProfileSharingAsync calls publicationStore.GetOrCreateAsync; deletion guard blocks any publication row; no remove-identity command. Choose A: read is side-effect free, first Publish explicitly creates permanent identity. Existing identities remain permanent after Unpublish; no deletion/recycling. Explain permanence before Publish and blocked Delete. |

Additional source finding: runtime ProviderProfileEditorModel currently has no expected concurrency token. Canonical entity tokens exist but generic editor updates do not compare the submitted revision. Add optional expected-token metadata for compatible existing callers, populate it for UI reads, enforce provided tokens and keep UI writes blocked until committed token reconciliation succeeds. This is a deliberate concurrency contract extension, not evidence that the old editor already enforced it.

Read-side tests, runtime projection, deletion reference and publication tests are useful baseline coverage, but fake desired outcomes and InMemory-only tests do not prove new transaction claims. New PostgreSQL production-adapter regressions are required.
