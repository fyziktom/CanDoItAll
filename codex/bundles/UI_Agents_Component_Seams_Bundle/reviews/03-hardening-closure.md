# SB08 hardening review and evidence

Date: 2026-09-05. Starting point: clean `components-decoupling` at `96ee03a97c510d5363636fb06b903b9bc12f47dc`. This report owns the follow-up requested in [the owner review](../inputs/05-owner-hardening-review.md). SB01-SB07 artifacts remain historical and are not claims of rerunning their gates.

Status: source hardening and its selected functional validation are complete. Repository-wide documentation closure is blocked by 118 pre-existing tracked proof logs. Do not describe every repository gate as green.

## Decisions and implemented behavior

| Review concern | Production correction | Direct evidence and shallow-pass trap |
|---|---|---|
| Save outcome taxonomy | `AgentEditorCommands` retains committed ID and warning from `AgentDirectoryProjectionSynchronizationException`; typed validation and concurrency are rejected; owner cancellation propagates; other workspace failures remain unconfirmed. | Unit `AgentEditorCommandsTests` distinguishes typed rejection, conflict, untyped InvalidOperationException, I/O failure and unowned cancellation. Integration `AgentEditorAdapterIntegrationTests` exercises the real workspace producer. A fake returning `Committed` alone would not prove the producer. |
| Known pre-write validation | Core wraps only the pure `AgentDefinitionFactory.Create` call with `AgentEditorValidationException`. Loading, catalog update and persistence remain outside this catch. | Actual duplicate template rejection followed by correction/save; existing thinking-effort factory cases remain covered. This is not a blanket conversion of every InvalidOperationException into a rejection. |
| Commit followed by a secondary failure | The known-commit projection boundary includes cache invalidation before and after synchronization, including cancellation after persistence. The editor retains the warning while allowing read-only reconciliation. | Real persistence plus projection failure, projection cancellation and each cache invalidation failure; composed editor warning survives a failed refresh and Retry without a second write. |
| Failed core load | Explicit Loading/Ready/Failed state; missing or mismatched agent identity and failed capability reads render the failure surface. Retry uses the same target. No form, Save or Clear while failed. | `AgentEditorLoadCharacterizationTests` replaces the unsafe acceptance oracle with two failed-load/recovery cases. Browser deletion in another tab creates a real missing-record failure. Provider/secret partial failures and ready-editor Clear remain covered. |
| Requested A -> null/invalid -> A | Clear the prior request-owned selection, publish null and rearm the presentation acknowledgment. An unchanged null parameter echo does not invalidate a manual selection acknowledgment. | `AgentCatalogBoundaryTests` covers both transitions; `AgentCatalogPanelTests` preserves no-duplicate manual selection/parent echo. Real same-document browser transitions clear the selected card and reopen one editor. |
| Host lifetime | Lifetime token reaches loads, mutations, chat and DialogService itself. Request/catalog generations and disposal checks fence late publications. | Pending reads, mutations, chat and dialog result cases. The final disposal/remount test keeps an unrelated dialog open while canceling this host's editor and permitting one replacement editor for the same target. Canceling only an awaiting task would leave an orphan presentation. |
| Pure draft policy | `ProviderModelValuePolicy` lives in Models. The draft policy and AgentFramework Razor compatibility helper use it. | Unit normalization and snapshot cases; source and dependency review. Generic Conversations UI does not gain an AgentFramework dependency merely to share trimming. |
| Mutable snapshot completeness | Retain the dedicated mapper and add a recursive public-contract sentinel guard. | `AgentEditorSnapshotContractTests` compares every public serialized value and recursively rejects shared mutable descendants. New unsupported property types fail explicitly. No private component reflection or serializer-based production cloning. |
| Stable sections | Read-only `AgentEditorSectionDefinition` list drives both labels/markup and index mapping. | Complete, unique, round-trip definitions plus invalid input guards; public tab and child component cases. No enum ordinal routing contract. |
| Read ownership | Move `ReadCapabilitiesAsync` to `IAgentEditorReads` and its adapter; keep the existing two cohesive ports. | Capability wizard uses the refreshed read fixture; actual registered reads tested in Integration. Reconcile remains the command's acknowledged-write recovery operation. |
| Duplicate IDs | Team dialog uses `agents-team-details-dialog-shell` and `agents-team-details-dialog-content`. | Actual composed DialogHost test and browser DOM each show one shell, one content and zero old IDs. Repeated collection card IDs remain collection selectors, not supposedly unique dialog IDs. |
| Test ownership | Move pure workspace queries and root preparation to Unit; real reads/catalog operations/EF/save-projection adapters to Integration. | Owning lane discovery and actual registered composition. Rendering, events and lifecycle remain in Components. No coverage was removed to reduce a count. |

The fully controlled host API was an optional design alternative. This repair preserves its public parameter/callback contract. Page/workspace remains authoritative for route-significant intent; the host retains an explicitly reconciled interaction mirror and presentation acknowledgment. Physical extraction and a new selection API would expand this repair without being needed for the reported transitions.

The new typed exception is at a real pre-write Core boundary. No new service interface, production registration, project reference, partial class, plugin layer or service bag was introduced. Snapshot mapping stays explicit, with an executable completeness guard. The generic Conversations selector remains byte-identical to the starting commit.

## Validation scope and commands

The selected Behavioral tier uses bounded consumers: editor/catalog contracts, Core definition validation, model normalization, workspace post-commit acknowledgment and relocated tests. There is no schema, root build, shared runtime composition or sibling change that justifies repeating the entire stable gate. The prior 9,597-case stable run is historical evidence only.

Run from the repository root. The compact [evidence record](04-hardening-evidence.json) records exact filters, expected/discovered/result counts, source hashes, local transcript hashes and CodeAnalytics snapshot identity. Raw logs, TRX and browser images remain under ignored `.mcp-state/agents-hardening-*` paths to avoid adding another large proof payload to branch history.

Each changed production project is built directly in Release with `dotnet build <project> -c Release --no-restore -m:1`: AgentFramework.Models, AgentFramework.Core, AgentFramework.Components and Modules.AgentFramework. The module is rebuilt after the final host cancellation correction. All four direct builds report zero errors and warnings.

The owning Unit, Components and Integration assemblies are built. For each selected lane:

```powershell
dotnet test <project> -c Release --no-build --no-restore --list-tests --filter <recorded-filter>
dotnet test <project> -c Release --no-build --no-restore --filter <recorded-filter> --logger 'trx;LogFileName=<lane>.trx' --results-directory .mcp-state/agents-hardening-results
```

Expected discovery is frozen in `.mcp-state/agents-hardening-selection.json`, including method and theory-case counts, and compared to actual discovery before running. Unit: 66 passed; Integration: 13 passed; zero skipped. The final 101-case Components selection passes with zero skipped after the dialog disposal correction. Total: 180 focused passing cases. The last change invalidated Components and its owning module build; unchanged Unit/Integration contracts retain their passing evidence.

Integration has one existing xUnit2029 build warning at `FileSandboxWorkspacePreparedCommitReadIntegrationTests.cs:30`, outside this diff. The initial moved tests failed DI until their real ApiTestHost enabled the same Razor interactive-server registration required by the production authentication-state adapter; the final 13 cases pass. Earlier component fixture failures were corrected: deferred capability reads, valid available team candidates, a providerless owned Retry target and an explicit mutation-start signal. The final report does not count these failed attempts as passes.

The browser exposed an additional cancellation defect: DialogService presentations outlived a host removed during database startup confirmation. Three public tests then failed against the actual pre-fix host (both team dialogs remained open; the editor result was not canceled). Passing the token into `DialogService.OpenAsync` replaces `Task.WaitAsync`, releasing the owned effect itself. This is failing-first evidence for a real lifecycle contract, not a missing-type compilation failure.

## Portability and documentation

Run scanner/enforcer self-tests, then scan the complete proposed source, including newly added unstaged protected files:

```powershell
python tools/Validation/Portability/test_enforce_portability_baseline.py
python tools/Validation/Portability/test_scan_artifacts_for_secrets.py
python tools/Validation/Portability/scan_portability.py --repo-root . --output .mcp-state/agents-hardening-portability-final.json
python tools/Validation/Portability/enforce_portability_baseline.py --scan .mcp-state/agents-hardening-portability-final.json --baseline tools/Validation/Portability/portability-risk-baseline.json
```

Self-tests pass (6 and 4). No baseline edits or intentional deltas were needed. Final enforcement after the last production change passes with all 14,251 reviewed executable-source findings unchanged; the evidence record contains its transcript hash.

`tools/Validation/Test-Documentation.ps1` fails because 118 tracked files match the generated/local-only log rule. `git ls-tree -r --name-only HEAD` and current `git ls-files` contain exactly the same 118 offending paths, with zero added or removed. The validator and these historical logs are unchanged. This repair neither suppresses the check nor renames/deletes proof to evade it. Both bundle manifests, JSON and local links are checked separately, since the documentation validator excludes bundle Markdown.

The owner's merge concern is valid: deleting temporary files in a later normal-merge commit does not remove their historical blobs. At the authorized merge checkpoint, choose a clean product-only result or an appropriate squash/history policy. No merge, history rewrite, staging, commit, push or sibling edit is performed here. This pre-existing documentation/branch-artifact issue remains a merge blocker, separate from the tested source repair.

## Runtime and browser evidence

Large desktop: 1600 x 1000, local Release Web DLL through the managed runtime, Development configuration, `https://localhost:7271`, configured PostgreSQL development profile. This is a local built-DLL check, not a publish/deployment or a watch timing measurement. The task-owned runtime is stopped after validation.

Observed production flows:

1. Continue with the existing startup database profile; catalog renders 29 existing technical agents.
2. Create one temporary agent, save with one editor remaining, update its summary, wait for the refreshed card, close/reopen and verify the persisted value. Closing while Save is pending is not used as evidence that Save completed.
3. Open its real persisted ID, close, navigate A -> null -> A within the same document. The card becomes unselected, then one editor reopens. Repeat A -> invalid -> A with the same result. Same-document marker confirms no full reload was substituted for this transition.
4. Open/cancel New team: one shell, one content and no old duplicate ID.
5. Delete only the task-created agent through the real confirmation UI in a second tab. The first tab keeps a stale catalog card. Opening that card shows `Agent was not found` with Retry/Close and zero form/Save/Clear elements. Retry retains the original requested ID. API catalog read confirms the task-created record is absent; no team is created.

Screenshots `catalog.png`, `editor-saved.png`, `deep-link-reopened.png`, `team-dialog.png` and `load-failed.png` live in `.mcp-state/agents-hardening-browser/`. The catalog, saved editor, team and failed-load screenshots were inspected: readable labels; ten visible section selectors; editor body owns scrolling and actions remain visible; failure actions appear in the first viewport with no editable draft; no new overflow or obscured action was found.

Browser limitations are explicit. Console errors around managed runtime stops are reconnect failures. A diagnostic GET of the deleted ID returned HTTP 500 with the existing `Agent was not found` exception; catalog GET confirmed deletion. Missing-record HTTP status translation is unchanged and is not claimed as repaired. The startup-confirmation duplicate presentation invalidated the earlier dialog-disposal proof and caused the targeted correction above; the final fresh-tab browser recheck shows zero editors while startup is paused and exactly one requested HR Agent editor after Continue. Its screenshot was inspected, and the read-only target was not changed.

## Architecture and next consumer

Live sibling mode is preserved. Components is `c3e6aa03a878994c0ba8aed6af017d0be75f3796` (CI pin match); FileTools is `7c7453c6583365ae5bd63f8fc6efc4a776e15818`, while CI pins `498b36825bd5a5222429972af120b04becf4b3f6`. Both are unchanged/read-only. Local source validation does not claim CI-pinned FileTools equivalence.

Scoped CodeAnalytics before/after snapshots and source review show no new dependency cycle or blocking diagnostic. The two existing cycles are the AgentFramework/Hosting module pair and ImageGenerationAgentRuntimeToolProvider/its nested builder. Factory-based registrations produce the same informational interpretation limitations. The scoped snapshot omits external project edges; its reference count is not evidence of a lightweight graph. No project files or production composition registrations changed.

Shared rules now explicitly cover one semantic authority, re-entry/echo transitions, cancellation of underlying effects, the four write outcomes, fail-closed core loading, temporary unsafe characterization, non-Razor policy ownership, public snapshot completeness, semantic section mapping, read ownership, test lanes and unique dialog IDs. See [shared feedback](../../UI_Component_Seams_Shared_Architecture_Bundle/reviews/02-agents-hardening-feedback.md).

Readiness remains differentiated: controlled catalog rendering and its interaction seam are proven; the hardened host/editor contracts have bounded tests and production checks; the complete editor subtree is not a lightweight extraction candidate. No UI assembly, standalone sandbox, production bookmarkability or measured watch speed improvement has been delivered. The next useful separately scoped step remains the controlled AgentCatalogPanel -> lightweight UI assembly -> catalog sandbox -> reproducible warm watch measurement.

## Final bundle audit

Both manifests verify (228 Agents entries, 27 shared entries), all 17 JSON documents parse, and all 163 local Markdown links resolve. The new evidence/authored-text secret scan passes: 36 text files, zero findings, no oversized or unreadable skips. Final source hashes match the working tree, git diff --check passes, no changes are staged, no project/build files changed, and historical proof files are unchanged.
