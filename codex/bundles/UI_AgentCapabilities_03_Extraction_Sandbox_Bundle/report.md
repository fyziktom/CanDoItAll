# Capabilities correctness, extraction and development-loop report

Status: CLOSED after the final byte-manifest and relative-link seal. This report distinguishes executed evidence from planned next work. No repository merge-readiness claim is made.

## Repository and evidence identity

Entry branch: `components-decoupling`; local and independently read remote HEAD: `b540b465cf408dd9c9adeb939538b78ced9bc7bb`. Parent: `d066d367d6abc147e046bee1308ea5f702e65cfd`; committed tree: `f8e127cffe93409efd13d741abfc7b191677658c`. Entry was clean. The index has remained untouched; this run's changes are owned unstaged/untracked changes. No commit, push or history operation was performed. The final receipt and source inventory separately identify committed blobs and the tested proposed bytes; a working-tree hash is not a commit.

Live Components remains at `c3e6aa03a878994c0ba8aed6af017d0be75f3796` on `codex/original-ui-refactoring-release`; FileTools remains detached at `7c7453c6583365ae5bd63f8fc6efc4a776e15818`. Both were clean and preserved. The different FileTools CI pin was recorded, not substituted. Machine: LUCYSPOWER, Windows 11 Pro 10.0.26200, i9-13900H, High Performance power plan; SDK 10.0.303, Node 24.19, Playwright 1.61.1 / Chromium 149.0.7827.55, viewport 1600 x 1000. Application Tailwind CLI 4.2.1 and live BaseLib's already-generated assets are preserved.

[Capabilities-02 post-push receipt](../UI_AgentCapabilities_02G_PreExtraction_Correctness_Bundle/post-push-receipt.md) checks 227 predecessor manifest entries and 13 originally prepared extraction entries against the committed tree. Historical closure and earlier proof retain their original observations.

## 02G adjudication and resulting behavior

All source findings have high confidence. [Adjudication](../UI_AgentCapabilities_02G_PreExtraction_Correctness_Bundle/adjudication.md), [exact semantic RED/GREEN map](../UI_AgentCapabilities_02G_PreExtraction_Correctness_Bundle/semantic-test-map.md) and [bounded closure](../UI_AgentCapabilities_02G_PreExtraction_Correctness_Bundle/closure.md) are authoritative.

| Finding | Adjudication and final behavior |
|---|---|
| A0 post-push receipt | Confirmed bookkeeping correction; add a receipt without rewriting the earlier staged-state closure. |
| A1 application state ownership | Confirmed; command/coordinator outcomes, eligibility, retention and Curator lifetime remain in Module. The host maps them to pure immutable rendering presentation. This is a source/dependency gate, not a fabricated runtime RED test. |
| A2 revision ordering | Confirmed; common AgentConfigurationVersion helper strictly advances accepted existing-agent configuration revisions beyond the prior revision, including equal/backward clocks and future imported timestamps. Proof observation remains LastVerifiedAtUtc. Rejected/superseded work and unchanged normalization do not advance it. Overflow fails before commit. No schema or second clock system. |
| A3 coordinator liveness | Confirmed; an escaping assignment/diagnostic command exception finishes only its own retained entry as non-active Unconfirmed. Reference fencing prevents an old completion from clearing a newer operation. No automatic replay. |
| A4 receipt-less diagnostic unknown | Confirmed; no exact diagnostic correlation exists in the legacy Task fallback. Explicit exact-attempt acknowledgement releases only the circuit block and states that the prior diagnostic may have executed. The next diagnostic requires new intent. Receipt-backed verification and immutable assignment retain stricter canonical read-only recovery. |
| A5 Curator unknown | Confirmed; the launcher supplies no stable proposed chat/session ID on failure, and persisted workspace session versus active-handle cleanup prevents proof of absence. Acknowledge only after inspecting managed chats; no launch, deletion or rollback claim. Keep any authoritative returned ActiveAgentChat. |
| A6 provider snapshot pairing | Confirmed public-contract defect; final capture pairs current catalog content with actual current CatalogDataRevision. A revision-observing snapshot implementation supplies direct evidence. |
| A7 initial infrastructure failure | Confirmed; catalog/profile/snapshot-source/composition failures before dispatch become InfrastructureUnavailable, not invalid input. HTTP 409 has a sanitized typed disposition and automaticReplaySafe=false; actual input rejection remains 400. Success and Task compatibility remain, with explicit 200/400/409 OpenAPI declarations. |
| A8 atomic presentation | Confirmed; read the coordinator entry once and derive operation and busy presentation from that same value. Curator also exposes one atomic snapshot. Source consistency correction, not a new framework. |
| A9 prerequisite closure | Passed before physical movement. Required regression, API, build, browser and static gates are linked from the closure. |

Additional direct findings were corrected narrowly: JSON copying a provider lease lost its readonly fingerprint; record-copy immutable lease fields and deep-copy only the mutable profile. Capability deletion and seed normalization could change existing agent configuration without advancing the revision; both now use the common helper only for changed existing identities. These changes do not introduce general capability CRUD recovery.

## Physical ownership and real consumers

[Movement receipt](proof/SB01/movement.json) records exact before/after hashes and paths. Seven files move into `src/UI/CanDoItAll.AgentFramework.UI/Capabilities/`: Surface Razor/code, pure State/contracts, renderer-owned Surface CSS, and the real AgentCapabilityList Razor/code/CSS. Old declarations and old CSS are removed. No copied component, backwards wrapper, service locator or broad convenience reference remains.

A public Curator action regression exposed an omitted pure child. [The child review](proof/SB01/avatar-child-review.md) justified moving the unchanged AgentAvatarActionButton from AppComponents to existing Conversations.Components, retaining its namespace/API. AppComponents now references Conversations; there is no reverse edge. External binary compatibility is not claimed; all repository consumers were rebuilt and the shared change triggered the stable gate.

The evaluated lightweight UI graph has 11 projects; the sandbox adds its host for 12. There are no cycles or edges to Module, Core, Persistence, ProviderManagement runtime, Voice, AppComponents or broad AgentFramework.Components. Scoped CodeAnalytics corroborates source shape but its generated/reference coverage is limited; evaluated MSBuild references are the dependency authority. Application services, recovery state and effects remain in the module.

Both AgentCapabilitiesSurface and AgentDetailsDialog render the same real moved list. Public component and actual Web browser checks preserve proof badges, assignment, explicit verification, details and accessible endpoint text. The final dialog browser run confirms existing-agent assignment saves and reconciles, enabling explicit verification; it does not launch a diagnostic automatically. The earlier harness incorrectly assumed draft-only assignment and is retained as a setup error, not semantic RED.

Renderer CSS owns one display:contents scope anchor and the existing tree/card scroll containers. Long-title and nested-card overflow were found by visual inspection and fixed at their smallest CSS owners with retained browser failures. No visual redesign or competing scroll owner was introduced.

## Sandbox, assets and direct observation

The existing sandbox now selects typed catalog/capabilities specimens. Missing or unknown specimen values preserve catalog defaults. Existing scenario/layout/agentId/teamId query normalization, reload restoration and replace-history behavior remain. Capabilities target failures remain explicit and never silently select another agent. The embedded baseline is byte-identical to the safe pre-move rendering snapshot.

Both modes execute the same 29 deterministic scenarios, covering loading/errors/missing/empty states, all kinds/proof states, long content, raw filters and access draft, pending/rejected/conflict/committed-warning/unconfirmed recovery, exact-before/adopt, preview and Curator/acknowledgement. Sample intents change only controlled state and the intent log. No workspace, persistence, provider, diagnostic, chat or external endpoint service is registered or invoked.

Parity links production CSS; Fast scans only the existing UI, sandbox and Conversations roots and uses live BaseLib assets. Separate build outputs and compiled/runtime mode guards remain. The real tree, cards, avatar, tooltips, fonts, icons and scoped styles are preserved.

A late browser comparison found a missing runtime-composed Split asset dependency. The first generated-class correction still failed because BaseLib's unlayered rule outranked layered utilities. The existing 25 production compatibility rules now have one shared unlayered Tailwind input, imported by Web and Fast; their old app.css block is removed. Both actual browser matrices pass with matching computed layout. [Adjudication](proof/SB03/responsive-utility-adjudication.md) preserves both failures. The first complete post-extraction timing series is retained as non-comparable; the entire post series is repeated using corrected assets and the unchanged timing harness.

## Validation and measurement

[Exact timing tables](proof/SB03/measurement-results.md) and [interpretation](proof/SB03/measurement-interpretation.md) report all accepted and excluded attempts. There are 216 accepted warm observations and 12 process-cold starts. Sandbox cold medians are 15.287 s / 14.952 s versus 58.628 s post full app. Forward CSS medians are 402.7 ms / 409.0 ms versus 3622.1 ms post full app. Razor/C# vary; Fast is not consistently faster, and full-app Razor did not improve in this sample. All source probes and generated assets are restored.

The 02G exact owning gate passed 267 cases (75 Unit, 80 Components, 112 Integration/API), plus 22 rendering cases. Its 25 new expanded RED/GREEN cases and direct API/file-store witnesses are mapped in the predecessor. SB00 passed 30 Unit / 49 Components; SB01 passed 93 Unit / 124 Components / 112 Integration/API; sandbox compatibility/scenarios passed 60 Unit / 38 Components, repeated after the final asset correction. These are overlapping owning runs, not additive unique-test claims.

The final named broad trigger was the shared avatar assembly identity and AppComponents project reference. Stable discovery had 10031 rows, expanding through seven reviewed public MemberData methods to 10086 executed cases: Components 1414, Integration 1402, MAF Memory 22, Memory 196, Unit 7052; zero failures or skips. Current discovery and current TRX were compared directly. Synthetic theory-secret arguments are redacted in retained evidence with raw fingerprints preserved; no test or scanner was weakened.

Direct Release builds covered Models/Core/Persistence in 02G; Conversations, AppComponents, UI, broad Components, Module, Web, both sandbox modes and all owning test projects during extraction. The complete product solution and stable solution builds passed. Every selected execution followed owning builds and frozen discovery. Final asset-only builds and four byte-identical production DLL hashes preserve the broad checkpoint's applicability.

Real Web browser paths cover normal capability rendering, assignment, two explicit diagnostics, details, recovery acknowledgement and one actual Curator chat with no replay. The final details consumer check separately exercises existing-agent assignment and authoritative reconciliation. Both final sandbox modes pass all 29 scenarios plus filters/reset/tree/access draft/query/history, endpoint accessibility, disabled states, real theme delivery and owned shutdown. Inspected images include normal, failed, warning, long, acknowledgement, Curator and details states. Historical evidence is not relabeled as a new execution.

## Limits and next work

Recovery is circuit-scoped and not durable across restart or a new circuit. Explicit acknowledgement does not prove absence or rollback. General capability CRUD recovery, first-create identity recovery, historical CapabilityDetailsDialog load fallback, provider redesign and other editor extraction remain out of scope. Production routes/bookmarkability are unchanged.

Capabilities-03 is closed. [Agents Overview-01](../UI_AgentsOverview_01_State_Read_Seams_Bundle/README.md), reference CDA-UI-SEAMS-AGENTS-OVERVIEW-01, is now prepared and unimplemented. [The Agent-module continuation roadmap](../UI_Agents_Component_Seams_Bundle/plan/04-agent-module-continuation-roadmap.md) is linked from the Agents umbrella. Preparation began only after the recorded Capabilities-03 seal; every Overview implementation child requires a separate owner instruction. Its inventory records 37 Unit, 12 Components and 1 Integration discovery rows from the already executed stable checkpoint as a reference, not a new Overview execution. The documentation-only follow-up leaves all 72 validated non-bundle source/asset paths unchanged.

Final static proof: portability 14383 unchanged reviewed findings, zero added/stale; six portability and four artifact scanner self-tests pass; source secret scan 245 existing/zero added; retained decompressed text has zero findings. Documentation retains exactly one inherited 118-log finding. See [validation](proof/SB03/closure-validation.json) and [repository receipt](proof/SB03/final-repository-receipt.json). No repository merge-readiness claim.

## Proven shared feedback and readiness

Shared architecture [revision/liveness feedback](../UI_Component_Seams_Shared_Architecture_Bundle/reviews/12-revision-ordering-and-explicit-liveness.md) records monotonic accepted revisions versus observation time, coordinator integrity after escaping commands, exact versus explicitly acknowledged unknown effects, and atomic presentation reads. [Rendered closure feedback](../UI_Component_Seams_Shared_Architecture_Bundle/reviews/13-rendered-closure-and-adversarial-content.md) records real child assembly resolution, adversarial content/geometry, equivalent measured frames and runtime-composed asset/cascade validation. These are implementation-proven lessons. Planned Overview choices are not added as universal architecture rules.

| Dimension | Final verdict |
|---|---|
| Capabilities semantic boundary | Closed; 02G corrections and owning recovery regressions pass. |
| Physical extraction | Completed; real Surface/list/CSS and pure child closure, application ownership outside UI. |
| Sandbox | Ready; existing host, both modes, 29 deterministic scenarios and preserved catalog query behavior. |
| Development loop | Reproducibly measured for frozen supported edits on this machine. Sandbox cold/CSS improve; full-app Razor and Fast-versus-Parity do not justify a universal speed claim. |
| Production bookmarkability | Not implemented/redesigned; existing routes preserved. |
| Next Agent slice | Overview bundle prepared only, including O00-O03 gates and exact proposed lifecycle proof. No production implementation. |
| Repository merge readiness | Not claimed. One inherited documentation finding covering 118 tracked logs remains. |

The final preparation [repository/no-drift receipt](../UI_AgentsOverview_01_State_Read_Seams_Bundle/inventory/current-receipt.json) independently rechecks local/remote HEAD, untouched index, all 72 validated non-bundle paths and clean unchanged siblings. Final owned-bundle hashes/links and decompressed retained evidence are checked after the documentation follow-up. No commit, push or cleanup was performed.

[Final owner-run validation](../UI_AgentsOverview_01_State_Read_Seams_Bundle/inventory/final-validation.json) records the post-preparation checks: 992 retained text artifacts / 285392657 decompressed bytes had no finding; source had 245 unchanged existing matches and no added finding; portability re-enforcement passes. The final small validation receipts and manifests are also scanned before delivery.
