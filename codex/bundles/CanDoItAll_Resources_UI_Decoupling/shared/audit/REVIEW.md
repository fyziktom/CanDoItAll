# Review · current architecture and changes to the shared base

**Prepared:** 2026-09-28. **Repository:** `fyziktom/CanDoItAll`. **Observed branch:** `development`. **Observed HEAD:** `7db3543ab437376baeca55089cb331fbe1b30483` (2026-09-27). The same branch HEAD was observed again at the end of source inspection. The user named the main repository, not the `main` branch; `development` was used as the current integration baseline for the ongoing refactoring.

## Conclusion

Continue incremental UI decoupling. The direction is consistent with the current modular application, but the supplied material should not simply be concatenated. Current maintained guidance and working implementation patterns have overtaken several historical assumptions. This package consolidates their UI-relevant rules, separates evidence from instructions and leaves module implementations to bounded child assignments.

## Material findings

| Finding | Source observation | Change in the new shared base |
|---|---|---|
| **Competing authority / stale sequencing** | The repo now points to `docs/architecture/ui-component-seams.md`; the old inputs still sequence historical Agents/integration work [S01, S02, S25]. | Canonical doc first; historical reviews optional; rebaseline each selected slice. No blanket pause until all domain refactoring finishes. |
| **More existing extraction than the old map suggests** | CRM/HR has a rendering library, contracts, a real sandbox, RecordBrowsing extraction and seven routed areas in its maintained completion map. Workflows and Simple Chats UI also exist outside `src/UI` [S05, S10, S15–S18, S23]. | Preserve/reuse these seams. “Remaining modules” is discovered by actual rendered closure, not by a directory convention or an old checklist. This is not a fresh runtime certification of those modules. |
| **One interface pattern would be a regression** | CRM uses a host-implemented workspace view; Prompt Gallery uses presentation/intents and sessions [S04, S11–S14, S22]. | Both are first-class options. Require a meaningful dependency/state boundary, not a prescribed controller or DTO hierarchy. |
| **EditContext ownership needs precise wording** | `CrmHrWorkspaceSurface.ContextFor` keeps a renderer-owned EditContext keyed to the host draft, despite shorthand guidance grouping draft/validation under the host [S01, S12]. | Preserve the draft's validation lifetime; do not move EditContext merely to satisfy a physical placement rule. Check real unmount/remount behavior separately. |
| **Build isolation is not proved by assembly names** | Source references are rewritten in `Directory.Build.targets`; a renderer can reference apparently light projects whose closure must still be inspected [S07, S10, S21]. | Check evaluated build graph, resolved runtime/package graph, rendered closure and assets/watch list separately. No automatic approval for `*.Abstractions`, no speed claim from fewer imports. |
| **Current boundary traversal is incomplete on unresolved references** | `CrmHrUiBoundary.TransitiveReferenceNames` catches `FileNotFoundException` and continues. Public-type classification also accepts the `.Abstractions` suffix [S24]. | Treat unresolved relevant closure as incomplete. New/changed guards need explicit ownership allow rules plus evaluated dependencies and negative cases. This is a test-strength limitation, not a demonstrated product exploit. |
| **Existing read-session code is a precedent, not flawless boilerplate** | `PromptGallerySearchSession` fences late reads and distinguishes a committed favorite from refresh failure; its per-operation CTS is cancelled/replaced but not disposed in the reviewed code [S22]. | Keep the proven intent while requiring disposal after each retired operation unwinds, including debounce cancellation. A resource-lifetime cleanup is a concrete follow-up, not an executed repair or a measured leak claim. |
| **Asset dependency is not backend compilation coupling** | CRM sandbox Parity links Web CSS as content; it references only the UI project. Fast has explicit separate CSS/output behavior and missing-mode/asset checks [S15, S16]. | Preserve Parity/Fast as separate asset modes. Permit explicit production-theme content without importing Web's compile graph; measure its generation/watch cost. |
| **Sibling provenance changed since the original integration** | CI resolves Components from the matching/target branch once per run, while FileTools is pinned to `498b36825bd5a5222429972af120b04becf4b3f6` [S19]. | Record both policies accurately. Do not recopy obsolete Components pins or perform package/version/merge work automatically. A merge into main uses the corresponding Components branch. |
| **Validation assumptions need updating** | SDK is 10.0.302; tests are separate from product solution; DB proof requires isolated PostgreSQL 18; CI has split/full platform shards; portability-static is mandatory for protected changes [S03, S06, S19–S21, S25]. | Narrow discovery-confirmed tests first, applicable real-owner/browser proof, mandatory static/documentation gates, wider gates by current trigger. No old universal full-suite loop. |
| **The domain Foundation must not silently widen UI scope** | The old Foundation covers many future runtime/operation protocols, not just UI; current instructions retain owner and control-plane boundaries [S02; input Foundation]. | Preserve authority and safe outcomes, but do not require implementing the full contract/receipt/outbox catalogue before extracting a stable renderer. |

## Current-source caveats to carry forward

The two code-level cautions above (reference-traversal completeness and CTS cleanup) were observed in source, not reproduced by a running test. They are explicitly named to avoid presenting every current implementation detail as the recommended reusable design.

The CRM completion record contains historical validation language and an earlier note about missing dedicated committed-read-back coverage; the current testing guide also lists `CrmHrCommittedReadBackTests` [S03, S05]. This review did not inspect/run every named case. Reconcile the actual test source before repeating the old “uncovered” claim or declaring it proven. A test name alone is not evidence of behavior or a pass.

The reviewed module project references identify coupling **candidates**, not proof that every component inside each mixed module is still tightly coupled. Conversely, a completed-looking rendering project is not proof that every production surface composes it correctly. See [module map](module-map.md).

## Suggested bounded maintenance follow-ups

Clarify canonical prose on draft/EditContext lifetime rather than assigning one physical owner universally. Strengthen the appropriate dependency-test helper at its owning test boundary without adding rigid file counts. Repair and regression-test Prompt Gallery request-source disposal when that seam is next touched. Refresh stale coverage wording after inspecting the named tests. None of these suggestions authorizes a broad repair of unrelated modules in a future UI task; a child explicitly includes any required prerequisite.

## Review scope and limitations

The supplied architecture/coordination documents and selected current source were reviewed. The source register records exact paths, repository commit, returned blob identity where available, and full/excerpt coverage. The review includes project metadata for the remaining mixed modules, actual CRM host/renderer/base-class code, a Prompt Gallery session, boundary test code, current build targets, sandbox startup and test/CI policy. Tree listings were used for discovery; a listed filename is not counted as code reviewed.

A local repository clone was unavailable in this environment, so source was read through the connected GitHub interface. No evaluated MSBuild graph, restored package tree, full-repository semantic scan, .NET build, application test, live browser journey, sibling implementation audit or performance experiment was executed. Prior proof reports were read as reports, not replayed. Product behavior and actual speed remain the implementation child's responsibility.

The package's own link/JSON/hash checks and helper tests are different, local validation; their actual results are recorded in [package validation](package-validation.md). No GitHub file, commit, branch or repository setting was changed by this task.
