# Migration and breaking-change matrix

## Decision rule

The target is the published **dotnet-1.22.0** release, including changes since the audited **1.20.0** baseline. Do not substitute upstream `main` or a later release silently. For each M-ID record actual package/symbol consumers, decision, implementation delta and proof. “Not applicable” requires a healthy inventory plus exact source inspection where dynamic registration or reflection could hide use.

Source IDs Uxx and Rxx resolve in [SOURCE_INDEX.md](reference/SOURCE_INDEX.md). NuGet metadata verifies the stable and A2A preview versions. Some MAF surfaces remain preview/experimental despite the stable core package; a minor-number bump does not imply semantic compatibility.

## Required core work

### M01 — Coherent package graph

Update the central MAF properties to 1.22.0 and the published A2A preview `1.22.0-preview.260918.1`. Reconcile all importers and direct pins, especially the MAF adapter and workflow adapter. The current AI 10.9.0, general Extensions 10.0.11, OpenAI 2.12.0 and OTel.Api 1.15.3 pins cannot simply be assumed compatible with the target floors. Target metadata specifies AI-family 10.10.0, relevant general Extensions 10.0.12, OpenAI 2.13.0 and OTel.Api 1.18.0. A2A targets A2A SDK `>=1.0.0-preview2`. [R04–R06, U03–U05]

Inspect production and test `project.assets.json`, lock files if owned by this repo, shared properties and direct test references. Do not suppress NU1605/NU1608 or install every latest package. Preserve provider-neutral project boundaries. Acceptance: no introduced unresolved/downgrade conflicts; actual runtime assembly/package versions recorded; relevant provider and reflection/serialization tests pass.

### M02 — Approval binding/replay, native state and batch lifecycle

**1.22 breaking:** #8375 plus #8432. Approval authority comes from the framework-surfaced request recorded in the native session, not a request reconstructed solely from caller-supplied history. The tagged binder matches/rebinds the approved tool call, consumes pending authority and handles settled call IDs and mixed approval requirements. [U06/U07/U08]

Inspect the entire chain R09–R13: native serialization, durable approval DTOs, tool-admission journal, continuation routing, finalizer, history and restart. Keep original request/call IDs, tool payload, policy/source identity, complete batch and consumed state coherent. A durable DTO is necessary application evidence but is not permission to synthesize the framework's internal pending state.

Test real tagged 1.22 streaming and non-streaming agents; accept/reject, mixed tool batches, duplicate response, unrelated user turn while approval is pending, native checkpoint serialization/restart, missing/corrupt state, cache eviction, settled history, and cross-project/session mismatch. Verify invocation/effect counts, not only response text. Test interrupted streaming because pending-state capture occurs at stream completion/disposal. Do not rely on a raw `IChatClient` path lacking a genuine active agent session as the approval boundary.

Required application changes: fix F01; establish tested 1.20-to-1.22 retained-state decisions under F04; represent rejected/expired/incompatible continuation honestly in UI. No private `_pendingApprovalRequests` writes, reflection injection, blanket disabled binding, or invented human consent.

### M03 — Per-run/dynamic tool behavior

1.22 moves ChatClientAgent tools to per-run delivery (#8531) and preserves harness middleware for dynamic tools (#8402). [U01; R13/R14]

Audit tool discovery/composition, admission, context-provider tools, approval wrappers, per-run options and capability caches. Do not depend on inspecting a globally populated FunctionInvokingChatClient to obtain current tools. Preserve registration ownership and middleware through dynamic refresh. Test two overlapping runs of the same agent with distinct project/tool scopes and same-named tools with different schemas. Assert correct toolset, authorization, receipts and finalizer for each run; no leakage between calls. Concurrency in the **test** is required; globally enabling concurrent mutable tool invocation is not.

### M04 — Workflow adapter semantics

Existing CanDoItAll workflows compile through imperative `WorkflowBuilder` [R16], so declarative release fixes are not a reason to rewrite them. Test compilation, typed input/output, explicit terminal outputs, branch/fan-out/fan-in, failure propagation, cancellation, native external input, checkpoint/resume, topology/version identity and handoff follow-up. Preserve stable executor/agent IDs and source-disclosure policy across reload.

If declarative packages are actually present, additionally cover formula-state concurrency (#8252), input serialization, HTTP header validation/canonicalization/redirect behavior (#8301/#8406 and 1.21 security changes). Declare the declarative-only tests N/A with source evidence if there is no such consumer. Never claim the formula fix by itself resolves current process escalation. [U01/U02]

## Conditional breaking surfaces — mandatory applicability review

| ID | Change | Exact migration obligation when used | UI/proof requirement |
|---|---|---|---|
| **M05** | 1.22 #7991: `AgentSessionStore` moved into shared Abstractions; delegating store moved into AI | Reconcile imports/references and store implementations. Shared key includes logical session ID plus named partitions. `GetSessionAsync` is nullable lookup; creation is explicit. Preserve independent session instances on lookup and all key dimensions. Do not conflate this API with the existing custom session envelope/store. | Two projects/users with colliding textual IDs stay isolated; missing session does not silently create during approval resume; assess old-key compatibility. [U09] |
| **M06** | 1.22 #8425: provider-backed MCP sessions per invocation | Applies to **declarative workflow MCP with a custom HTTP-client provider**, including tools/list, even when provider returns the same client or null. Default no-provider caching is distinct. Respect transport/session ownership; never dispose caller-owned HTTP clients. Check R14 rather than replacing its independent client layer wholesale. | Two different credential contexts never share a cached authenticated MCP session; owned resources released after success/failure/cancellation. Session continuity/cost changes documented when relevant. [U10] |
| **M07** | 1.22 #8434: Foundry delegated identity sticks to AgentSession | Replace per-call `WithFoundryHostedAgentUserIdentity` usage with session creation via the supported `CreateFoundryHostedAgentSessionAsync` identity option. Do not retarget an existing session to a new delegated user. Requires actual Foundry consumers; do not add Foundry for this task. | Persist/restart continues as the original user; identity boundaries remain separate even when sandbox IDs coincide. [U11] |
| **M08** | 1.21 #8032: A2A hosting run modes | `DisallowBackground` → `ReturnMessage`; `AllowBackgroundIfSupported` → `ReturnTask`; `AllowBackgroundWhen` → `ReturnTaskWhen`. These describe response shape, not underlying agent background capability. Search hosting consumers/configuration and serialized enum names. R15 is an A2A client, not by itself evidence of these hosting APIs. | Preserve configured message/task behavior, streaming and terminal task states; versioned settings migration only if old names are persisted. Test remote skill failure and resource lifetime (F03). [U12] |
| **M09** | 1.21 #7671: file line contract and `file_access_read_lines` | .NET line positions are 1-based; CRLF/LF/lone CR terminators are preserved, with no phantom trailing empty line. Search match text includes terminators. Custom AgentFileStore/FileMemoryProvider overrides must align with their `ReadAsync` content. `SearchAsync` is now virtual with a base implementation; existing overrides still own conformance. | Grep → read lines → replace targets the same text on Windows/Linux. Test CRLF/LF/CR, empty/trailing newline and stale `ExpectedLine`; errors must not disclose unreadable content. Refresh tool descriptions/schemas if exposed. [U13] |
| **M10** | 1.21 #8159: LocalCodeAct environment isolation; approval/OS hardening | If LocalCodeAct is actually registered, explicitly allow only required environment values through supported options; no broad inherited secrets. Preserve execution approval and host-capability restrictions. Application-owned shell/FileTools drivers are not automatically this package. | Unsupported OS/capability is diagnosed before side effects; required approved tools still work on both platforms. Do not enable LocalCodeAct merely for upgrade coverage. [U02] |
| **M11** | 1.21 #8290: MCP skills archives ZIP only | Inventory actual imports and templates. Unsupported TAR/tar.gz/tgz must not be advertised as supported by that upstream path. Convert only owned fixtures/configuration where appropriate; retain independent formats supported by a separate application importer. | Setup/catalog shows unsupported archive accurately rather than a usable empty capability; ZIP and path containment tests. [U02] |
| **M12** | Skills/security fixes, hosted identity and diagnostics | Revalidate skill resource/script paths at use (#8151), frontmatter parsing (#8430) and documented caching behavior (#8474), hosted storage/session boundaries (#8146/#8263), redirect headers (#8164), PowerShell exit state (#8259), token accounting (#8334), public telemetry source name (#7815) where actually consumed. | Traverse/symlink/outside-root denial, cancellation/cleanup, safe diagnostics, provider/session identity and accounting remain correct; no scope or secret leakage. [U01/U02] |

## Do not confuse optional adoption with compatibility

`AsIChatClient`, per-tool AgentModeProvider controls, new Foundry integrations and replacing custom persistence are **not required new features**. Adopt only when a demonstrated local simplification preserves all contracts and stays within scope. Do not combine this migration with a general tool-parallelism rollout. Any workaround removal needs a regression proving the tagged framework supplies the missing behavior at the actual application boundary.

## Closure artifact

For every M-ID report: observed consumers/package, disposition, source anchor, changed files or explicit no-change reason, tests selected and actually run, UI implication, and remaining limitation. “Build passed” alone never closes M02, M05, M06, M07 or M09.
