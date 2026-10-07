# Projects Files P2 execution evidence

This records the bounded execution of
`codex/bundles/CanDoItAll_Projects_Files_UI_Decoupling/prompt.md` on 2026-10-01.
The [architecture record](projects-files-ui-decoupling.md) describes the final owners
and renderer census. This is feature evidence, not application release certification.

## Source and environment

| Input | Actual entry |
|---|---|
| Application | `components-decoupling`, `47651dd679a14e706a764a76ff84ba197a90ccb4`, clean |
| Components | `4a858412d2c2a3f6123bf23d8c4584f05b47627d`, clean; development publication verified |
| FileTools | `3a080ecd31068a77c1e1bd639f7a78e21c93db85`, clean |
| SDK / dependency mode | .NET `10.0.303`, evaluated local source references |
| Build and test configuration | `ProjectsFilesProof` |
| Browser | Chromium, exclusively 1920 × 1080, 100% zoom |
| Owned database | PostgreSQL 18.6, labeled task container and loopback relay; private credential file |

The application entry is newer than the package's reviewed P1 commit
`c207b499c3165ba95e46ffaf518884097d3135dd`. No reset or forced pin was used.
Components and FileTools source remained unchanged. The normal application on port
5032 was not used as a fixture. Live inference flags were disabled and no paid request
was authorized. CodeAnalytics, Components and dotnetwatch MCPs were unavailable;
actual source, evaluated MSBuild, CLI discovery and owned `dotnet watch` were used.

Private raw evidence is retained at
`artifacts/projects-files-p2/20261001-47651dd6`. `entry.json`, the before/after graphs,
test result sidecars, discovery logs, TRX files, owned-resource receipts, browser logs
and probe files identify the actual inputs. They are not checked-in runtime logs.

## Work-unit and raw-input closure

| Raw requirement / work unit | Shipped behavior and meaningful proof | Disposition |
|---|---|---|
| First repair P1-R1 | Exact native pre-write seed/delete refusal preserves an acknowledged save, retires the old draft and releases only its mutation slot. Same public ID can acquire its new lifetime. Four failing-first native cases; 14 owning controls pass, including unknown/pending protections. | Solved |
| First repair P1-R2 | Pending import and loaded module have separate ownership. Handled import failure is not rethrown by teardown; a late module is released once without stale validation/focus/save. Actual modal failure reproduced, then seven interop cases and all 18 P1 leaf cases pass. | Solved |
| Complete both Files surfaces | The entire dialog and portfolio markup lives in Files.UI. Actual FileBrowser and read-only FileInteraction render source/search status, retry, empty/error, preview/back and action feedback. Neutral renderer proof uses real sessions/content. | Solved |
| Preserve P1/Workspace and file authority | All 39 existing UI/sandbox closures and packages remain unchanged. Existing coordinators, grants, trusted paths and action service retain authority; no new registry, path fallback or HTTP owner. Native and unit controls exercise refusal and valid access. | Solved |
| Validate lifetimes | Held A→B→A acquisitions and cleanup, late success/failure, stale snapshots, changed contexts, independent previews, cancelled construction and primary-plus-cleanup failures are exercised. Native portfolio tests inspect actual providers, scope maps, revision and usable content. | Solved |
| Validate content/downloads | Seven valid fixture formats render; production Markdown uses the explicit registered viewer composition. Browser transfers verify saved bytes, SHA-256 and safe names. Common runner/guard tests verify stream, lease and JS cleanup and no replay. | Solved |
| Complete independent sandbox | Real bounded synthetic providers and registered viewers; two independent surfaces; held/failed/removed sources; source and independent publish proof. Actions are explicitly simulated. | Solved |
| Affected Agent/Workflow journeys | UI-created projects use actual runtime tools, approval, file writes, readbacks, attachment and original produced-file identity; the same asset opens/downloads in Projects. Only the external model response is scripted. Five Agent proposal refusals and incomplete Workflow output refusal pass. | Solved |
| Desktop, development loop and final gates | Only supported large desktop is exercised. Current graph/watch, restored edit probes, static enforcement and closure manifests are recorded below. | Solved |

The shallow alternatives are explicitly rejected: a one-line forwarding leaf, fake
browser markup, mutable old-workspace updates protected only by an outer generation,
profile lookup against invalid image/PDF bytes, and a copied Agent/Workflow fixture
would not satisfy these controls. Production contains no fixture-specific branching,
new `TODO`, `NotImplemented` or replacement authority path.

## Exact test protocol and current results

Each selection was build-discovered with its exact filter and expected case count:

```powershell
dotnet test $project --configuration ProjectsFilesProof --list-tests --filter $filter /m:1
dotnet test $project --configuration ProjectsFilesProof --no-build --no-restore --filter $filter /m:1 --logger trx --results-directory $ownedResults
```

The owned wrapper sets the task PostgreSQL connection privately, checks the container's
ownership label, disables live inference and rejects a discovery-count mismatch before
execution. `--no-build` is used only against the preceding matching build. The exact
project/filter/configuration/discovery/exit/time data are in each `*-result.json`;
`test-results-index.json` records TRX hashes and individual outcomes, including failures.
Counts below are selections, not a sum of unique tests.

| Selection / artifact prefix | Project under `tests` | Filter (`FullyQualifiedName~` for each term joined with `\|`) | Discovered / result |
|---|---|---|---|
| `s0-refusal-before` | Components/CanDoItAll.Tests.Components | Seed_transaction_refuses_the_original_admission_after_same_id_recreation \| Delete_refusal_preserves_the_replacement_and_releases_only_the_original_operation | 4 / 4 failed before repair |
| `s0-native-verified` | Components/CanDoItAll.Tests.Components | ProjectsEditorMutationTests | 14 / 14 passed |
| `s0-js-observed-before` | Components/CanDoItAll.Projects.UI.Tests | Failed_import_is_reported_once_and_actual_teardown_allows_a_fresh_editor | 1 / failed before repair |
| `p1-leaf-final` | Components/CanDoItAll.Projects.UI.Tests | CanDoItAll.Tests.Components.ProjectsUi | 18 / 18 passed |
| `p2-neutral-verified` | Components/CanDoItAll.Projects.Files.UI.Tests | CanDoItAll.Tests.Components.ProjectsFilesUi | 17 / 17 passed |
| `p2-neutral-frozen` | Components/CanDoItAll.Projects.Files.UI.Tests | CanDoItAll.Tests.Components.ProjectsFilesUi | 17 / 17 passed after final menu classes and explicit initial fixture state |
| `p2-host-and-download-final` | Unit/CanDoItAll.Tests.Unit | ProjectFilesSurfaceSessionTests \| ProjectFilesActionInteropTests | 14 / 14 passed |
| `p2-owning-unit-final` | Unit/CanDoItAll.Tests.Unit | ProjectFilesSurfaceSessionTests \| ProjectFilesActionInteropTests \| FileAccessAuthorizationTests \| FileToolsIntegrationBoundaryTests \| FileToolsDownloadLeaseTests \| FileToolsBrowseItemActionServiceTests \| NativeProjectAdmissionContractTests \| ProjectFileFilterProjectionTests \| ProjectPackageHardeningTests \| ProjectPackageStorageCatalogSnapshotTests \| ProjectManagedStorageDeletionTests \| ProjectsPageLoadGenerationTests | 158 / 158 passed |
| `p2-native-final` | Components/CanDoItAll.Tests.Components | ProjectsPageTests \| ProjectsEditorMutationTests \| ProjectsFilesHostLifecycleTests \| ProjectsAgentChatContextProviderTests \| FileToolsHostActionsTests \| ProjectStructureAttachmentPreviewDialogTests | 87 / 85 passed, 2 obsolete viewer assertions failed |
| `p2-native-viewers-verified` | Components/CanDoItAll.Tests.Components | Project_files_pilot_opens_authorized_markdown_after_browser_replacement \| Project_files_portfolio_opens_authorized_file_after_browser_disposal | 2 / 2 passed after asserting the actual Markdown viewer |
| `p2-resources-neighbor` | Components/CanDoItAll.Resources.UI.Tests | CanDoItAll.Tests | 70 / 70 passed |
| `p2-workspace-neighbor` | Components/CanDoItAll.Workspace.UI.Tests | CanDoItAll.Tests | 52 / 52 passed |
| `p2-sandbox-verified` | Playwright/CanDoItAll.Tests.Playwright | ProjectsFilesSandboxBrowserTests | 2 / 2 passed; source and published |
| `p2-production-consumers-verified` | Playwright/CanDoItAll.Tests.Playwright | ProjectsPortfolioBrowserTests \| File_harness_rehearses_read_write_attach_two_readbacks_and_preview_with_delayed_approval \| Workflow_UI_real_runtime_preserves_asset_with_only_external_model_scripted | 3 / both consumer cases passed; Portfolio obsolete viewer assertion failed |
| `p2-production-shell-verified` | Playwright/CanDoItAll.Tests.Playwright | ProjectsPortfolioBrowserTests | 1 / passed after the same viewer correction |
| `p2-menu-final` | Playwright/CanDoItAll.Tests.Playwright | ProjectsFilesSandboxBrowserTests \| ProjectsPortfolioBrowserTests | 3 / 3 passed with the final generated CSS and full-menu geometry requirement |
| `p2-desktop-and-consumers` | Playwright/CanDoItAll.Tests.Playwright | Above three topics, ProjectsFilesSandboxBrowserTests, File_harness_rejects_changed_proposals_through_actual_UI_without_widening_grants, Workflow_UI_rejects_http_success_with_incomplete_model_output_before_creating_an_asset | 11 / 8 passed, 3 failed; all six refusal controls passed |

All executed cases in these selections have zero skips. The mixed 87- and 11-case runs
remain mixed; they are not relabeled green. Repaired cases are separate current proof.
The native set includes all four new actual-owner cases (overlap success/failure,
grant-construction cleanup and 64/65 source bound). The unit set includes actual
authorization, exact native launch requests through a controlled launcher, download
lease revocation and unavailable headless actions. No arbitrary external application
was launched by the tests.

## Causal corrections and retained failures

- S0's first weak teardown test passed because bUnit captures renderer-disposal errors.
  Observing `Renderer.UnhandledException` reproduced the fault. Initial helper errors
  querying already-disposed rendered state were repaired; the original results remain.
- Host lifetime tests exposed context capture after held cleanup. Capture now precedes
  that wait, and a changed context refuses the original operation. Successful empty
  source sets skip FileBrowser initialization and retire previews explicitly.
- The held real-browser source-removal test initially propagated its expected
  cancellation. It now observes cancellation and then verifies that late completion
  cannot revive the removed source. The failing observation remains in `p2-leaf-bounded`.
- FileInteraction defaults to its built-in composition unless passed the composition
  parameter. Passing the neutral host composition fixed actual Markdown/Mermaid output;
  old plain-text-only assertions were corrected to the genuine registered viewer.
- The browser focus probe recorded Enter reaching the dialog Close button during close.
  Waiting for actual dialog removal and asserting row focus corrected the observation.
- Full Chromium renders the valid PDF; the bundled headless shell reported a PDF plugin
  error. The sandbox uses full Chromium, checks actual blob bytes, and the inspected
  `pdf-loaded.png` shows the native PDF page. This is not a claim about unsupported shell
  PDF rendering.
- A simulated read error was initially non-retryable, so the real browser correctly hid
  Retry. Marking that deliberately retryable fixture error fixed the scenario.
- Final overlay review found the pane action menu clipped at the desktop's right edge.
  Before geometry was x=1849.20, width=160 in 1920 pixels; positioning tries moved it to
  x=1721.20. The browser assertion now requires the entire menu in the viewport. Both
  renderers apply that local positioning rule; generated Tailwind CSS is rebuilt.
  The prior partial-visibility pass and strict three-case failure remain recorded.
  The first generated selector also required escaping literal underscores; its build
  was stopped before test execution. The corrected served selector and final three-case
  pass are recorded separately, with no failed attempt overwritten.
- Final source build found the fixture callback's state initialization could not be
  proven non-null by the compiler. An explicit initial state removes that warning;
  the subsequent 17-case neutral run rebuilt the sandbox and passed.
- Discovery mismatches and a build attempted while an owned host held output DLLs are
  retained as failed commands. Builds and live hosts are subsequently serialized.

These are explained repairs, not timeout retries reclassified as product fixes.
Historical P1 timeout and selector uncertainty remain historical limitations.

## Desktop composition, publication and loop

The actual file list or read-only preview is the primary surface. Source/search controls
and compact count badges support it; they do not displace it with metric cards. The
portfolio pane retains its available-height browser scroll region; the full task dialog
uses its body as scroll owner, with title/Close visible. Read-only viewers stretch past
640 pixels in the representative production dialog and pane. No unrelated narrow-screen
layout was introduced or tested.

Inspected evidence includes the production Agent/Workflow Markdown previews, the native
PDF page, the independent dialog and actual action menu. The late menu finding reopened
overlay proof rather than being dismissed by DOM visibility. Source/published sandbox
screenshots and served asset lists live under `output/playwright/projects-files-p2`.
Both hosts load actual material fonts, isolated CSS, FileTools viewers and Mermaid.
Publishing uses `dotnet publish --no-build --no-restore /m:1 /nr:false --configuration
ProjectsFilesProof --output <owned-directory>` and runs the resulting DLL outside the
source directory with Production environment and no database connection.

The current graph has 183 evaluated projects. All 39 previous UI/sandbox closures and
packages are identical. P1 UI/sandbox remain 5/6; new Files UI/sandbox are 8/11; Web is
164→165 and the module 33→34. The P1 watch set is exactly 300→300; Web 4597→4606;
Files sandbox 479. No project-reference cycle, unresolved edge or forbidden transitive
backend dependency was found; neutral tests include negative controls.

| Owned host | Hydration seconds | Razor first / warm / warm | C# first / warm / warm | Scoped CSS |
|---|---|---|---|---|
| Before extraction Web | 2.72 | 2.37 / 1.85 / 1.85 | 1.71 / 1.80 / 1.30 | No feature-owned CSS |
| After extraction Web, before final menu class | 4.16 | 3.90 / 2.37 / 2.36 | 1.40 / 2.40 / 1.36 | No feature-owned CSS |
| Independent Files sandbox | 0.45 | 1.33 / 0.83 / 0.83 | 0.68 / 0.71 / 0.68 | 0.69 / 0.63 / 0.48 |

The measurements use normal watch, explicit interactive readiness and three samples;
C# visibility is observed after an explicit refresh/reset because existing state does
not reconstruct itself on Hot Reload. Probe bytes are restored and hashed. No
feature-owned JavaScript was invented: the download module stays in AppComponents.
These local observations do not support a uniform whole-Web speedup claim.
The watcher reported applied C#/Razor deltas and static asset updates; no polling mode
was used. It also emitted existing static-web-asset manifest parse warnings in Web;
those logs are retained, and successful served-asset/browser checks are separate proof.
No browser tests or competing builds ran during the timed edits. Web's initial watcher
build took 68.36 seconds; navigation hydration above excludes that build/startup work.

`validated-inputs-final.json` identifies 1,424 source files, 13 product/test binaries
and three key generated/static assets by SHA-256. It compares the earlier test checkpoint
to final source: local menu classes, stricter browser geometry/selector observations,
and explicit initial fixture state. The owning production C# and native authority inputs
are unchanged. The final three browser cases cover changed menu assets, source/publish
and production downloads; the final 17 neutral cases cover the small fixture change.
All probes restored their original bytes; later intentional edits are distinguished
from failed restoration in that manifest.

Selected final SHA-256 values (full paths and the remaining binaries are in the manifest):

| Artifact | SHA-256 |
|---|---|
| Projects module DLL in Web output | `dde4cdd91913ccfa79aa23a32a46eb343e1f62ddf25b12e51c345600881f6d0f` |
| Files.UI DLL in Web output | `4de05ee423ae0fbb1c9c49ed91eafb71185842bec905264993410bde6d6c5b99` |
| Files sandbox DLL | `cc5b0abc33253a2a22fabfc87720d5076706c1ccce6d6df11ca6aecfa5172186` |
| Native Components test DLL | `4ebd730150be85eec0fe15bda4ce9fafd9aa85ddcc9ecdf468df5a76a149fd41` |
| Unit test DLL | `f0c4ee274909f0dae1c0d4533cd2184ac2b06ce5596818705518529334739747` |
| Playwright test DLL | `3e7714c03e26092fba89bf523bfddc90049cdd27769732e49a78145de8511e9f` |
| Generated application CSS | `d5dd4ebd71584cc6067dab21942aafa5fbe5d0b6464fea927e0a6badc7f79fcb` |

## Final gates and limitations

The impact decision in the architecture record bounds this feature with owning and
neighboring proof. Broad Stable is not repeated: no shared SDK/build anchor, broadly
consumed public owner contract, schema, file authority or core session protocol changed.
Root/CI additions only register the new isolated projects. Generated CSS is the existing
application asset pipeline, not a changed shared theme contract. Historical Stable is
not promoted to current P2 evidence.

Portability review inspected every added/stale finding. The reviewed refresh replaces
25 old fingerprints (27 occurrences) with 52 new fingerprints: FileTools namespace/type
references, neutral packages, README code-fence/text matches and escaped Tailwind selector
backslashes misclassified as a Windows path. No OS assumption, hard-coded product path,
shell elevation or new process launch was introduced. The baseline diff was inspected;
final enforcement **without `--write-baseline` passed with 15,241 allowances**. Scanner
and artifact-secret tool tests passed (6 and 4 respectively). Patterns and validators
were not weakened. The full scan includes new untracked protected files and is not
truncated. Earlier failing deltas remain in the evidence directory.

Final `dotnet build <project> --no-restore --configuration ProjectsFilesProof /m:1`
passed for Web (including Projects and both UI leaves) and the independent sandbox.
The later neutral build removed the sandbox warning. Task-owned watchers and their
listeners have exited; the labeled PostgreSQL container and relay are stopped.
The owned volume/private raw evidence remain for audit. No user process was stopped.

Documentation validation passes for 332 maintained Markdown files. Its initial missing
test-project README finding was repaired, and the failed result is retained. The final
staged-source portability scan also passes without a write flag. `git diff --check`
passes after removing a trailing blank line in a new test file; no executable behavior
changed in that whitespace correction.

The shareable evidence directory contains 210 selected artifacts plus a manifest of
exact copy hashes. The unchanged secret scanner checked 203 text files with zero
findings, zero oversized/unreadable text and the private credential sentinel; eight
screenshots are non-text. A prior raw-tree scan found the literal credential-variable
reference in the private execution helper and skipped four oversized source-scan
exports. Those raw inputs remain private and unchanged; the shareable whitelist omits
helper bodies/runtime data/source excerpts rather than claiming the entire raw tree is
clean. No scanner exception or rule change was added.

The manual compatible-bundle gate is **Pass for S0 and Projects Files P2**, with all 18
working evidence groups closed. The sealed package remains an input, not a rewritten
status ledger. The final status agrees with code, source/publish/browser proof and the
explicit bounded wider-test decision. S0 is signed local commit
`0f4cbe82ee15a52b545278510df495b5fdc0f369`; the P2 source is the signed local commit
containing this record. Neither was pushed by this task.

The package is a compatible sealed external shape, so its supplied validator plus a
manual semantic gate is used instead of migrating it into a canonical bundle layout.
The sealed tracked inputs remain byte-identical. Its initial self-test generated a
Python cache, and automatic approval review rejected its cleanup as "blocked by policy".
The cache remains; no alternate deletion was attempted. An isolated exact `git archive`
copy passed 14 package tests, 14 shared tooling tests and the 44-file / 18-group /
60-link / 28-source validator. This does not claim a clean in-place cache inventory.

The inherited selector remains `REVALIDATED_WITHOUT_ROOT_CAUSE`, the 40/40 live journal
is unchanged, and paid/provider/generated-application prerequisites remain open. No
push, PR, merge, deployment, package publication or third module is part of this closure.
