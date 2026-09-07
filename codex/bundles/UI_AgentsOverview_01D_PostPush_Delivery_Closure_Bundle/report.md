# Phase A delivery report

Reference: CDA-UI-SEAMS-AGENTS-OVERVIEW-01D. This report concerns proposed bytes. It does not claim that any change has been committed or pushed. The root manifest and final verification seal the proposed-byte Phase A closure. Governance entry is authorized only after that seal; this Phase A report contains no Governance execution.

## Repository identities and delivery order

Primary entry and latest observed local/remote HEAD are `2f3a6020e805deb02a6dcfbdfb52f352eb59ce61` on `components-decoupling`, tree `6b112718e6892ce2113b71dfaa71b170637e3f0c`. Components local HEAD is `c3e6aa03a878994c0ba8aed6af017d0be75f3796` on `codex/original-ui-refactoring-release`. Its independently refreshed remote main is `bd1eb1030c438c861b94b3b8d3b9dba72925d685`; both committed trees are `8ee42b3f1f97fd4bb7b3b3f526029228d8ed6e23`. The prompt's different remote observation was not used as a pin. FileTools stays clean and detached at `7c7453c6583365ae5bd63f8fc6efc4a776e15818`, tree `6bc360281b6ad13ddec5e813f0a00e26b5bc7d6d`. [Final identity receipt](retained/A06/closure-identity.json).

Both mutable repositories have clean indexes and only inventoried owned working changes. No staging, commits, pushes, branch cleanup or CI implementation occurred. The unrelated primary worktree was left intact. FileTools received no source changes.

The real order is Components review/commit/push first; primary review/commit/push referencing the available contract second; clean post-push verification third. Updating the CI pin is separately owned and remains necessary. The current pushed primary state cannot compile independently against the committed Components API. [Owner verification commands](plan/owner-verification.md).

## Pushed source and retained proof

All 46 recorded final Overview source inputs correspond to the pushed primary source: 45 exact byte matches and one line-ending-only difference. This preserves the architecture claim, not a claim that the uncommitted sibling contract is published. [Pushed comparison](retained/A00/pushed-source-comparison.json).

Allowed retention strategy 1 keeps the exact historical Overview and Governance evidence. At entry, 1,152 Overview and ten Governance manifest entries were absent from Git but available locally. No historical proof is missing locally. Narrow ignore exceptions make them retainable; scoped `-text` attributes preserve exact evidence bytes through Git clean filters. The original manifests and corrected document before-images are retained compressed. No old execution was relabeled or recompressed. Five fresh README provenance notes explain retained fixture directories required by the documentation checker. [Entry retention inventory](retained/A00/retention-inventory.json).

The new validator checks membership in tracked plus non-ignored proposed files, every root hash, every sealed member and Markdown relative links. It rejects line-ending/filter conversion of sealed bytes before commit; actual-tree mode reads only Git blobs. A temporary Git fixture proves that local ignored evidence cannot satisfy an immutable-tree check. Sixteen tooling self-tests pass. [Final proposed-inventory verification](retained/A06/final-verification.json) covers the retained root seals and links.

## Production corrections

The primary display label is now exactly `Agents · Overview` or `Agents · Governance`. Two direct tests failed on the malformed committed label and pass after correction; both exact labels were inspected in the real Web browser. Three bounded historical documentation corrections preserve intended punctuation, with before/after hashes. Strict UTF-8 auditing covers the predecessor diff, both complete bundles, current production text and changed Components text. No localization transliteration or unrelated line-ending pass was performed.

The only other primary production change gives the existing team editor one lifetime token for its read, save and nested icon picker. Closing/removing that editor cancels only its picker and fences late UI results. A real DialogHost behavioral test failed with the orphan overlay before the change and passes afterward. The browser confirms Catalog removal removes both team editor and picker. Save semantics were not redesigned.

Components owns a reference-counted `PreserveDialogsOnSamePageNavigation()` lease. Without a lease, the prior navigation-close behavior remains. With an active lease, the same canonical URI authority/path preserves reference, result task and cancellation registration across query and fragment changes. A different path, path case or trailing slash closes dialogs. Multiple owners release independently and idempotently; explicit close and cancellation still work. Service disposal detaches navigation once and cancels remaining references. No global state or router abstraction was added.

The pre-canceled-token witness exposed a separate narrow registration-order defect. Adding the reference before registration, and disposing a registration that completed synchronously, prevents an orphan already-canceled dialog. The exact five Components files are:

- `src/CanDoItAll.Components.BaseLib/Components/Modals/DialogService.cs`
- `src/CanDoItAll.Components.BaseLib/README.md`
- `tests/CanDoItAll.Components.BaseLib.Tests/DialogNavigationOwnershipTests.cs`
- `tests/CanDoItAll.Components.BaseLib.Tests/fixtures/approvals/standard-public-api.metadata.approved.json`
- `tests/CanDoItAll.Components.BaseLib.Tests/fixtures/approvals/standard-source-package-inputs.approved.txt`

[Dialog contract](architecture/dialog-contract.md), [actual Agents owner audit](architecture/dialog-audit.md), [proposed source hashes](retained/A06/proposed-source.json).

## Cancellation adjudication

All 15 delayed-registration characterizations passed without an Overview production change: six session lane cases and nine usage-dialog cases cover replacement/disposal, late success/failure/cancellation and old-finally fencing. Cancellation is prompt, delayed registration does not escape ObjectDisposedException, and resource disposal/idempotent ownership remain observable. These are negative regression evidence; no generic cancellation framework or deferred-disposal redesign was justified. [A05 closure](retained/A05/closure.md).

## Independent source and package reproduction

Clean Git archives plus only inventoried owned overlays built Module and Web successfully with explicit sibling roots. FileTools had zero overlays. The initial Windows long-path setup failure is retained; a verified task-owned short mapping allowed the actual build. All compiled owned inputs still match the successful isolated build. [A04 closure](retained/A04/closure.md).

Common and BaseLib packages at unique local version `0.3.0-overview01d.20260907193451` restored, compiled and executed in a neutral external consumer, with exactly the two intended packages and zero project references. No package was published. A further attempt packed all seven required Components packages and built the isolated primary Module with source substitution disabled. It failed only on unavailable FileTools 0.3.0 dependencies. This optional package-graph limitation does not invalidate the independent API consumer or source-mode proof. [Actual package adjudication](retained/A04/primary-package-followup/adjudication.json). The temporary drive mapping has been removed after ownership verification.

## Validation status

Direct builds pass for BaseLib, its tests, Usage, AgentFramework.UI, Module, Web, both existing sandbox modes, Unit, Components, Integration and the real Web browser fixture. Current exact owning runs pass 80 Unit, 88 Components and four Integration cases, plus 14 sibling navigation and seven publishing/API cases. The initial 39-case RED/GREEN checkpoint overlaps these current selections; it is not added to their unique total. [Build commands](retained/A06/direct-builds/commands.json), [owning selections](retained/A06/owning-tests/selections.json), [owning results](retained/A06/owning-tests/results.json).

The shared cancellation-registration behavior is the named broad stable invalidation. Fresh stable execution passed all 10,254 cases: 7,115 Unit, 1,516 Components, 1,405 Integration and 218 across the two Memory projects, with zero failures or skipped cases. All 8,030 method identities are accounted for. The 10,199 discovery rows expand by 55 runtime cases through the same source-verified deferred theories; six existing sanitized-display differences are also explicitly reconciled. The complete run took 110 minutes. [Stable result](retained/A06/stable/stable-summary.json), [exact discovery reconciliation](retained/A06/stable/discovery-verification.json). A read-only stack snapshot caught active file persistence during the extended integration stage; it does not establish a performance cause. An earlier slow focused component run was mistakenly interrupted by the operator; the exact 88-case rerun passed in 7m34s without source changes. An overly short exploratory timeout is also retained as incomplete setup evidence, not a product failure.

Real Web passed 32 scenarios; Parity and Fast each passed 18 assertions with 25 captures. Selected screenshots and DOM were inspected for the corrected labels, same-page query/fragment/Back/Forward, Defaults completion, nested team ownership, actual page departure and both sandbox renderings. These use the actual Web and controlled existing sandbox fixtures, not a replacement application. [Browser inspection](retained/A06/browser/inspection.md), [reproduction](retained/A06/browser/reproduction.md).

Portability enforcement passes without baseline writing after three reviewed validator-only findings were added (14,388 allowances total). Six portability self-tests and four secret-scanner self-tests pass. Documentation fails on exactly 118 inherited tracked log paths, with no new documentation issue. No log was deleted and no checker weakened. Complete primary and Components source comparisons, all three retained-bundle scans, strict encoding and the final inherited-error comparison are in the [closure checks](retained/A06/closure-checks/summary.json). [Static commands](retained/A06/static-final/commands.json).

## Timing applicability and remaining gates

No measured Overview renderer, project input or generated asset changed: 31 production/rendering inputs, 14 project files and both measured CSS assets match. The prior 12 cold starts and 216 warm observations remain historical evidence for those exact inputs. They were not rerun and are not a fresh speed claim. Prior pre/post full-app and Parity/Fast medians and all failures remain in the original proof. No universal Fast advantage is asserted. [Measured input comparison](retained/A06/measured-inputs.json).

At the Phase A closure, Governance G00-G03 has not started. Its session, selection, allowlist, deterministic time, shared timeline/metrics ownership, physical movement, sandbox and measurements remain unimplemented in this follow-up. Diagnostics preparation is gated on Governance manifest closure and has not begun; Diagnostics production is unimplemented. Production bookmarkability is unchanged.

Phase A closes as proposed-byte delivery once its final root-manifest check passes. Repository merge readiness remains blocked by unpublished sibling bytes/pins and the inherited documentation debt. Optional primary package mode is limited by FileTools package availability. These limitations are not converted into passing results.
