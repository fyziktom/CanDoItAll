# Agent Editor Core A1 boundary

This slice extracts the form shell and Identity, Runtime, Images and Voice. Projects P1
and Files P2 remain complete for their selected surfaces. It does not complete every
Agent editor section or all AgentFramework rendering.

## Rendering and ownership census

| Surface or effect | Renderer | Authoritative owner |
|---|---|---|
| Form, ten section identities, loading/error/retry, mutation feedback, Save/Clear/Delete footer | `AgentEditorCoreSurface` in Editor.UI | Existing `AgentEditorSession`, reads and commands in the module |
| Identity, tags and avatar display | Editor.UI and `ConversationIdentityFields` | Canonical host draft and existing Favorite policy |
| Runtime provider/model/effort, status/workload/history/tool/approval values | Editor.UI and neutral Conversations selectors | Existing safe mapper and thinking-effort policy; original confirmation owner |
| Images and Voice | Editor.UI | Existing complete submission, recommended provider selection and native save |
| Memory | Actual `AgentMemorySettingsPanel` in a typed production slot | Existing memory profile reads and draft settings |
| Project Structure Access | Original module markup in a typed production slot | Existing lazy project reads, selection and lifetime binding owner |
| Workspace Tools | Original module markup and actual storage picker in a typed production slot | Original risk confirmation, external-root and storage restrictions |
| Secrets | Original module markup in a typed production slot | Reference metadata only; credential resolution stays outside the renderer |
| Process Access | Original module markup in a typed production slot | Existing unavailable selection behavior; no new Process capability |
| Capabilities | Original module markup in a typed production slot | Existing create/verify/assignment and shared save admission |
| Avatar generation and shared-provider refresh | Real host integrations in typed slots | Existing provider-backed generation and source refresh owners |

The production entry point remains `AgentCatalogHost` opening `AgentDetailsDialog`, including
route-requested targets. Direct/embedded hosts use the same component. CRM-HR navigation and
projection remain host-owned. Every slot runs under the same logical form/draft/context;
retained sections are not blank production substitutes.

`AgentEditorCoreState` carries safe provider/model and effort presentation plus typed intents.
Callbacks capture the originating session. The renderer captures current state when each
retained tab fragment executes, avoiding one-render-old policy guidance without remounting
the tab, form or deferred children. Identity uses immediate shared input controls for Unicode
and Enter before blur. That option defaults off for existing conversation consumers.

The old MAF effort component remains a compatible wrapper. Its presentation policy is
single-sourced beside the original policy owner and produces the neutral generic contract;
Editor.UI never imports the broad MAF Components/Core/Voice/Canvas graph. Saved unsupported
overrides remain visible. Provider default and explicit None remain distinct.

No save/authority protocol or schema changed. The complete draft is captured by the original
policy before awaiting the command. Original ID/version, deferred lists, project lifetimes,
host bindings, opaque extensions and hidden settings retain their native owners. Read-back
retry remains a read. A source refresh additionally checks the captured provider selection
revision, so an old read cannot publish after A→B or A→B→A.

## Graph and asset proof

Evaluated MSBuild contains 185 projects across the full census. Editor.UI has an 11-project
closure; its independent sandbox has 12. All 41 existing UI/sandbox reference and package
closures are unchanged. Public signature and assembly guards include forbidden-transitive,
EF and unresolved-edge negative controls. No runtime owner gains an upward editor reference.

The initial directory-based reverse-edge check flagged `Foundation/CanDoItAll.Migrations.PostgreSql`.
Source inspection confirms it is the existing migration aggregation root through
`App/CanDoItAll.Composition` and the full module catalog. Those edges already existed and are
unchanged. Its product closure naturally includes the new module renderer; it is not a new
runtime-owner reference or a reason to move migration ownership in this slice.

Section/load presentation types retain namespace, member names and values; the module forwards
their old assembly identities. Existing persisted model contracts do not move. Renderer scoped
CSS moves with the core. The sandbox publishes BaseLib theme/font assets, the existing generated
product theme and scoped styles without product DI/database. There is no feature-owned JavaScript.

Watch sets are Web 4,606 → 4,617 and the new editor sandbox 534 paths. The completed P1 and Files
sandbox sets remain exactly 300 and 479 paths respectively. No SDK, package version, template
behavior, source/package mode or global watching setting was changed.

## Validation and evidence

Execution receipts are under `artifacts/agent-editor-a1/20261001-92a3c373/`. Raw failed attempts
remain distinct from later passing runs. Package-author NOT_RUN input templates are unchanged.
The standalone source and published sandbox browser cases pass at 1920×1080, including native
widgets, Unicode Enter, effort/default distinctions, unavailable catalog entries, read-only
reconciliation, two lifetimes, viewport/focus/footer, scoped assets and fonts.

The first host selection passed 103/107. Three failures identified stale effort presentation
in retained tab fragments, repaired at the rendering boundary. The fourth identified an early
test dispatch before footer registration; the test now waits and dispatches in the renderer.
The focused ten-case follow-up passed. Initial new leaf failures exposed blur-only input and
an incorrect test graph name; the corrected 16-case leaf selection passed. Later sandbox
failures were assertions against a dropdown's internal index and display-text casing; both
actual source/published cases subsequently passed. Native and browser fixture comparisons were
also corrected to compare tag sets and permission list contents rather than incidental order
or list reference identity. None of these changes weakened production owners.

The browser empty-name attempt is a separate reproduced product defect, not a flaky toast check:
the old host persisted an empty name and its chat-context renderer then threw. The repaired
Enter/correction journey passed against the same native owner. Discovery initially expected
188 component cases; six conversation shell cases live outside the Conversations directory.
The corrected source census and fresh discovery both reported 194 before execution.

## Final checkpoint

All final selections used SDK 10.0.303, source-mode sibling references and `AgentEditorProof`.
Changed Conversations, Editor.UI, MAF Components, AgentFramework module, sandbox and Web projects
were built directly before build-backed test discovery. No cases were skipped in these runs.

| Owning selection | Discovered / passed | Receipt prefix |
|---|---:|---|
| Agent editor/session/catalog/provider and neutral conversation components | 194 / 194 | `a1-final-owning-validated` |
| Command, snapshot, draft and provider/model/effort policies | 144 / 144 | `a1-final-unit` |
| Native commands, reads, cache failure and CRM projection outcomes | 12 / 12 | `a1-final-native` |
| Independent renderer, scenarios and public/dependency guards | 16 / 16 | `a1-final-leaf` |
| Existing Simple Chat definition editor consumer | 15 / 15 | `a1-final-conversation-consumer` |
| Source sandbox, published sandbox and UI-saved-agent/file journey | 3 / 3 | `a1-final-desktop` |

The earlier eight-case browser batch remains **7 passed / 1 failed**. Its five authority-negative
cases, timeout/retirement control and Projects/Workspace handoff passed; the positive case
exposed the blank-name defect. The subsequent three-case pass includes the repaired positive
journey, not a relabeling of the old batch. The added blank-name admission guard cannot affect
those negative cases: they submit named agents and their authority/file owners did not change.

The positive journey saves all four core sections through the actual route/dialog, cancels the
real auto-approval confirmation, reopens the saved High effort, and executes the saved agent in
Project Structure chat. Six scripted external responses were consumed by the real provider,
tool and approval pipeline. Native receipts confirm one file write, one attachment, exact parent
identity, two read-backs, an unchanged sibling asset and a matching downloaded SHA-256. The
produced file is also previewed/downloaded through Projects Files. No paid/live calls occurred;
the exhausted 40/40 journal is unchanged. Image generation and audio effects were not invoked.

Native core-only round trips cover both an agent and a template, preserving the entire deferred
configuration: permissions, secret references, project IDs and lifetimes, process IDs, actual
external-root bindings, storage restrictions, memory settings/bindings/assignments, capabilities,
Favorite and other tags, hidden temperature/history/background settings and unknown JSON.
The old update version is rejected and a separate agent remains byte-for-byte unchanged.

At 1920×1080, production Runtime and Voice screenshots were inspected for readable content,
all ten tabs and accessible footer; the published sandbox was also inspected. Source/published
browser checks verify keyboard input, focus, viewport, close/reopen and asset delivery. Published
assembly/PDB/CSS/font hashes are retained before the temporary host is disposed.

## Development-loop observations

These are three ordered edit-to-visible observations in seconds, not a controlled speedup claim.
The successful original-host baseline followed an interrupted earlier attempt and was warmed.
No expensive build/test ran concurrently with the measurements. C# probes required an input
rerender; Razor and scoped CSS were observed automatically. There is no feature-owned JS.

| Host | Razor | C# | Scoped CSS | Interactive page setup |
|---|---|---|---|---:|
| Original product editor, warmed repeat | 1.867 / 1.339 / 1.595 | 1.057 / 1.326 / 1.434 | 5.969 / 5.037 / 4.757 | 2.320 |
| Extracted editor in product | 3.187 / 2.386 / 2.929 | 1.069 / 1.863 / 1.596 | 7.330 / 17.812 / 4.650 | 3.220 |
| Independent editor sandbox | 2.919 / 1.880 / 1.324 | 0.270 / 0.264 / 0.266 | 0.703 / 1.034 / 0.531 | 0.365 |

The original warm Web build took 60.07 seconds; the final pre-probe direct Web build took
30.51 seconds under different incremental conditions. Startup, build and edit measurements
remain distinct. Web watch emitted SDK static-asset parsing warnings, but actual hot reload,
served assets and all probes succeeded. The original failed probe attempt and bounded file-write
retries remain in the raw evidence. Every probe restored matching SHA-256 bytes. Rebuilding
Editor.UI, sandbox and Web afterward produced **75/75 identical recorded binary/PDB/style hashes**
and identical changed-source hashes, preserving the final test provenance.

## Static gate, source pair and resource disposition

Portability-static reviewed six added and two stale findings: origin-fenced existing MCP callback,
moved avatar URI classification and portable README command fences. The intentional baseline
delta was reviewed; final enforcement without `--write-baseline` passes **15,245** findings.
Scanner rules are unchanged. Documentation/evidence tooling and package integrity are recorded
separately from native C# tests. The 44-file supplied package and its 20 NOT_RUN template groups
remain immutable and untracked.

The entry application commit was `92a3c373c742537608fded3c483b685291339853`; S0 is signed commit
`8ab6493e6dee489f9f3c13bc91dbc2f215f67e11`. Components remains
`4a858412d2c2a3f6123bf23d8c4584f05b47627d` and FileTools remains
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`, both without sibling edits. The signed A1 commit
contains this record; its resolved identity, source hashes and compiled artifacts are captured
in `final-binary-source-provenance.json`. No push, merge, package publication or other editor
slice is included.

The task-owned watch process trees are stopped using recorded PID, creation time and launch
command checks. Browser/test hosts dispose their own servers and temporary roots. The owned
PostgreSQL/relay pair is stopped after validation, with its labeled volume/network and private
diagnostic roots retained. The ordinary host on port 5032 and unrelated resources are untouched.

The working run directory contains `evidence.json`, `raw-attempt-index.json`,
`final-binary-source-provenance.json`, graph/watch comparisons, loop observations, TRX hashes,
static review/enforcement and resource receipts. Raw failed attempts stay local; the safe index
contains hashes/counts rather than credential-bearing logs or complete provider fixtures.

## Wider validation decision

This is a bounded feature checkpoint. Root solution and CI-list additions register only the
new isolated renderer and test assembly. Presentation-only section types move with assembly
forwarders; no persisted enum/model protocol changes. The shared identity option retains its
old default, and the effort wrapper retains its API and policy. Source refresh fencing changes
only this editor's publication, not source reconciliation or provider authority. The read fixture
adds an opt-in held-provider delegate without changing ordinary fixture behavior.

These are bounded by actual graph/public-signature controls, direct production builds, current
owning/native/caller tests, negative authorization controls and real browser consumer effects.
The browser raw-input control also reproduced an inherited empty-name save: native persistence
accepted the trimmed empty value and the chat-context entity renderer then threw. A bounded
guard in this editor's shared save admission now refuses blank names before the native command,
retains raw input and leaves the editor writable. Both empty/whitespace native host controls and
the browser Enter/correction journey cover this guard. No persistence schema, general command
validation or shared host policy changes were introduced by that repair.
They do not change shared composition registration, build anchors, persistence/migrations or
common test-host policy. Under the named-trigger rule, broad Stable is not repeated. Historical
P1 Stable is not claimed as A1 evidence. Full portability-static remains a separate mandatory gate.

## C# Architecture Gate Result

Status: **PASS** for S0 and the selected A1 boundary, with the bounded owning/consumer evidence above.

The change uses a presentation contract and typed intents at an actual assembly boundary, with
the original canonical session and native commands. It does not introduce a duplicate mutable
whole-agent DTO, service locator, arbitrary callback dictionary, runtime factory or policy copy.
The substantial retained code-behind owns the six deferred sections and original effects; the
selected rendering itself resides in the leaf. The independent scenario host is a separate
composition and contains no production mock branch. Later editor slices remain out of scope.
