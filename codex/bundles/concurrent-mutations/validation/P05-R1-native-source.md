# Resource R1 native source checkpoint

Authored in the managed checkout, 2026-10-17. Source-only: no build, discovery, test execution, browser session, database access or migration generation was performed for this checkpoint. The immutable [native readiness review](../reviews/P05-R1-native-cutover-readiness.md) records the prior design; this document records its implementation. Parent owns integrated validation and migration generation.

The native registry now captures owner context and a stable create candidate when opening a draft. Its original expectation is independent of the editable project and connector proposal. Project changes preserve the draft and `EditContext`; connector changes advance a configuration generation. An accepted snapshot advances the exact accepted base before local editor projection, route publication or reference refresh. Reconciliation uses that snapshot and submitted value/version witnesses, with no post-save exact read. A failed local projection retains the acceptance, allows draft editing, and disables another Save until explicit reload.

Reads distinguish Found, Missing and bounded MetadataOnly. Metadata-only, unavailable-connector and governed rows retain an exact deletion header without decoding unsupported configuration or inventing a current project. Pending/Unknown block the original context/resource identity, including the preallocated create candidate, across reselection within the controller. Current Found/Missing observations do not clear that hold or change the original expectation. Explicit New starts an independent draft; it does not resolve older evidence.

Promotion captures the original catalog context and candidate. Created/Reused snapshots are retained before callbacks or source reopening. Reused renders the stored name and sensitivity separately from the requested proposal. A refusal-shaped callback failure after acceptance becomes a committed warning. File actions and their authority remain with their existing owners.

## Source-derived validation selection

| Assembly / filter | Facts | Theories / InlineData | Expected rows |
| --- | ---: | ---: | ---: |
| `CanDoItAll.Resources.UI.Tests`, all tests | 53 | 11 / 26 | 79 |
| `CanDoItAll.Tests.Components.ResourcesExactEditorHostTests` | 0 | 1 / 2 | 2 |
| `CanDoItAll.Tests.Components.Shell.ResourceFileBrowsePaneTests` | 7 | 0 | 7 |
| `CanDoItAll.Tests.Components.OwnerPostcommitPageTests` | 4 | 5 / 14 | 18 |
| `CanDoItAll.Tests.Components.ResourcesPageUrlStateTests` | 3 | 0 | 3 |
| `CanDoItAll.Tests.Playwright.Smoke.ResourcesBrowserTests` (separate browser lane) | 2 | 0 | 2 |

The ordinary selection is **109 rows** (79 lightweight + 30 monolithic Components). Browser adds **2**, separately owned and executed. No MemberData, custom test attributes or skips occur in these selected sources. The exact-editor and URL-state classes carry HostPlatform; both browser Facts carry Playwright and HostPlatform. The remaining selected classes have no category exclusion. Full-class selection intentionally retains the unchanged TestLab rows in the shared postcommit class.

Lightweight per-class counts: Boundary 3, Browse 23, ContractCompatibility 2, EditorReadiness 10, OriginalState 8, Registry 20, PageLoadGeneration 2, SurfaceTab 1, Surface 10. The eight new Facts use the real controller/rendering boundaries with the existing deterministic scenario store; they do not prove PostgreSQL CAS or Search atomicity:

1. Accepted base advances while post-dispatch project, connector, raw configuration and edit context survive; the next explicit Save uses the exact accepted expectation.
2. Local `CreateEditor` failure retains acceptance and editable draft, disables Save, and needs explicit read-only reload before another submission.
3. Another stored edit after acknowledgement does not replace that acknowledgement or silently supply the next expected revision.
4. New registry and promotion drafts retain their opening dataset context after a dataset change.
5. Oversized unbound metadata deletes by exact header without editor projection or current project admission.
6. Unknown update/delete stay blocked after Found/Missing observations, including attempted reload or another write.
7. Reused promotion retains the exact stored metadata through a later refusal-shaped notification failure.
8. Raw capture bounds refuse before owner dispatch and preserve the draft.

Existing host fixtures now use typed context/expectation/command/outcome APIs. Resource Search fault rows deliberately change meaning: Search is enlisted before commit, so they require no effect and ordinary refusal. Activity fault rows continue requiring committed identity plus warning. The delete conflict fixture now performs a real intervening owner save and checks its changed revision; changing a proposed project is no longer a valid way to alter the original deletion expectation. Backend proof owns actual SQL transaction and migration guarantees.

The existing sandbox browser Fact additionally exercises held Save, post-dispatch typing, accepted draft preservation, sticky Unknown after observation, and metadata-only unbound deletion. Its existing close/dispose arrangement remains. Production browser assertions use exact immutable read snapshots. These are prepared source, not browser proof; screenshots and layout review are pending parent execution.

## Review and transfer boundary

Independent persistence peer reviewed both controllers, draft reconciliation and the eight new Facts and found no source blocker. This does not establish compilation or runtime behavior. The reconciliation of derived ConfigJson/Location assumes those fields remain non-editable in the renderer; their source configuration inputs have version checks.

Roslyn syntax-only inspection of the changed C# sources reported no syntax errors; owned-path `git diff --check` passed. No compiler, Razor compilation, portability scanner or runtime check was executed here. Required parent validation remains standard owning builds, exact source-counted discovery/execution, both browser Facts with visual review, and the full final no-write portability/rendering gates. Existing historical register corrections are parent-owned and must be preserved.

Merge only the three Resource rows in [the rendering delta](../reviews/P05-R1-native-rendering-entries.json); do not copy the whole managed register. The [predecessor capture](../reviews/P05-R1-native-predecessors.json) was recorded before source edits. Final owned paths, successor hashes, dependency hashes and selection sources are in the native source inventory.

No new dependency, project, generic state framework, automatic retry, Resource receipt/replay protocol or SPA activation was introduced. Pending evidence remains controller-local: disposal/navigation does not establish an unknown outcome, and durable navigation-safe unknown recovery is not claimed. Retained-data compatibility and Resource migration proof remain separate owner gates; this source checkpoint does not qualify those gates.
