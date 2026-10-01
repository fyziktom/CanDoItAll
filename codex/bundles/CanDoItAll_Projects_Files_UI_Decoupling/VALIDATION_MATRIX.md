# Validation matrix — current code, current discovery, actual layers

Each group is an obligation, not a prescribed test count. Derive counts from current sources, confirm discovery with the exact build configuration and filter, and record runtime theory expansion separately. The template starts at NOT_RUN. Fake-owner tests, neutral sandbox tests and actual backend/browser proofs do not substitute for each other.

| Group | Required controls and acceptance |
|---|---|
| E0 | Current branch/sibling pair, clean/dirty baseline, exact P1 and existing UI closures, owned test-resource inventory |
| S0 | Known seed and Delete admission refusal; truthful stage, retained prior acknowledgement, explicit fresh lifetime acquisition; genuine unknown and pending cases still locked correctly |
| S0-02 | Failed JS import followed by Close/reopen; pending import success/failure/cancellation during retirement, loaded module released once, no stale validation/focus/save |
| P2-01 | Renderer/public-contract/evaluated graph boundary, forbidden transitive and unresolved-edge negative controls; P1 and Workspace roots unchanged |
| P2-02 | Full dialog/pane parity: actual controls, empty/loading/error/retry, title/status, filter/source/search, preview/back, read-only mode |
| P2-03 | Open/reset/close/dispose: held cleanup, A→B→A, independent dialogs, late success/failure, no successor callback, exact resource disposal |
| P2-04 | Immutable filter snapshot and overlapping actual source-set updates; browser providers, scope/action maps, revision and current location agree |
| P2-05 | Failed refresh vs successful empty; explicit retry; late old snapshot cannot replace a newer accepted source or clear its failure |
| P2-06 | Real current file authority: removed key, profile/actor/source changes and revoked grants refused; permitted source remains usable |
| P2-07 | Independent preview lease across browser component removal; all valid supported fixture viewer families, cancellation after grant, complete cleanup with injected failure |
| P2-08 | Existing native launch requests and unavailable modes; exact preferred-app/containing-folder targets under current authority; no actual arbitrary external executable launched |
| P2-09 | Browser download saved bytes/hash and safe filename; grant/stream/JS cleanup on success, failure and retirement; no replay |
| P2-10 | Source64/page50/progressive budgets, source-native sort, Unicode/search/paging, proper cancellation and bounded call counts |
| P2-11 | Source and independently published sandbox; real viewer registrations/assets/fonts, two simultaneous surfaces, no DB/runtime dependency |
| P2-12 | Real UI-created project + Agent/file and Workflow/TestLab consumer; read-back identities and bytes, refusal controls, no paid inference |
| P2-13 | P1 full editor, package/hierarchy/deletion and neighboring Workspace/Resources regression; route/hydration/navigation acknowledgement and logs |
| P2-14 | Before/after graph/watch/startup/edit-to-visible observations, restored probe files; 1920×1080 functional visibility |
| G1 | Current owning tests and builds, named wider-test decision, portability-static no-write enforcement, source provenance, Projects census and honest closure |

## Existing owning suites to locate and retain

- `tests/Components/CanDoItAll.Projects.UI.Tests`: existing draft/form/boundary proof; new Files tests should not expand this old graph just to reach production.
- `tests/Components/CanDoItAll.Tests.Components`: `ProjectsPageTests`, `ProjectsEditorMutationTests`, `ProjectsAgentChatContextProviderTests` and relevant Resources/file-owner consumers.
- Existing `ProjectFileFilterProjectionTests`, native project admission/deletion/package tests and the FileTools integration/authority/lease tests identified by actual callers.
- `tests/Playwright/CanDoItAll.Tests.Playwright`: `ProjectsPortfolioBrowserTests`, existing file Agent harness and Workflow/TestLab journey, plus new Files sandbox/lifecycle/download cases.

Place new neutral Files renderer/scenario tests in an appropriately light test project. Register it in Components/Stable and every relevant current CI selection; never add test projects to the product solution. Do not inherit an old expected count after adding cases.

## Failing-first tests

S0 must demonstrate the wrong UI disposition/reacquisition behavior, not just the already-correct native rejection. S0-02 must demonstrate the previously handled failed import rethrown by actual modal disposal, then the successful fresh-editor recovery. P2 timing controls should first distinguish the old host behavior where possible. Hold meaningful owner/cleanup/browser boundaries with TaskCompletionSource or the existing controlled infrastructure, not arbitrary sleeps. For stale result tests assert absence only after the delayed operation/event actually finishes. Re-query and dispatch bUnit events on the renderer as current test guidance requires.

For source replacement test the mutable workspace itself. A test that only checks `activeProjectionFingerprint` or that a fake returned the newest list is insufficient. For leases assert old access fails after release while another valid view still reads. Include primary-failure plus cleanup-failure ordering.

## Browser and evidence rules

Use 1920×1080. Preserve screenshot and geometry evidence for the true renderer; DOM text is not visibility. Wait for current interactive readiness and actual operation completion; URL or disabled state alone is insufficient. Isolate browser contexts across origins and retain complete owned-host logs through shutdown. Test a controlled non-cooperating read without disposing a running user's context.

Actual supported FileInteraction viewer fixtures must contain valid format bytes. Do not claim a PDF/image viewer works from a profile lookup against arbitrary three-byte content. Keep existing profile-selection unit tests, but supplement them with genuine rendered fixture content.

## Wider gate decision

P1 already completed an expensive broad checkpoint plus a separate owning Unit rerun, with explicit final-source differences. It is not a green proof for new P2 source. Nevertheless do not repeat the full historical campaign at entry or after each phase. Use the current named invalidation rules. If this run materially changes shared file-owner semantics, public broadly used contracts or common infrastructure, stabilize the source and run the required wider checkpoint. Otherwise document the bounded final owning selection and neighboring tests. In either case portability-static is mandatory and final enforcement must omit `--write-baseline`.

A later targeted pass never silently overwrites the original failure. Report exact commands, configuration, discovery/executed/passed/failed/skipped counts, artifact hashes and source/test binary identity. Source scanner findings in discovery artifacts must retain private raw provenance and an explicitly transformed safe derivative; never weaken the scanner or delete a failing test to create green evidence.
