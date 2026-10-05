# Agents presentation completion AC1

AC1 completes the scoped Agents presentation boundary. SCH-R1 and WF1-R1 are
repaired, the final native consumer campaign passes, and the current census has
no remaining product renderer. Workbench and Processes extraction has not started.

| Closure field | Result |
|---|---|
| `agents_ui_boundary_complete` | `true` |
| `scheduler_restart_fixed` | `true` |
| `preview_attempt_identity_fixed` | `true` |
| `native_consumer_campaign_passed` | `true` |
| `ready_for_next_module` | `true` |
| `release_ready` | `false` |

The frozen Stable run remains **failed: 16,489 passed, one failed, zero skipped**.
Its single retained historical synthetic-key finding is classified below; it is
not an unresolved product blocker or a green full-suite claim. Source, task-delta
and full-worktree secret scans retain their distinct scopes and dispositions.

Current work executes the sealed AC1 package on `components-decoupling`. The entry
main is `dba71b3db4a2ae99b5d837e8df2c9df37dc4af4a`. Components local and remote
development both resolve to `2eccdddd05a9b1b0c90935ddee49dc559fd5eec1`, including
the Tooltip and read-only Canvas repairs. FileTools local and remote main are
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`. The unchanged CI pin
`498b36825bd5a5222429972af120b04becf4b3f6` is an ancestor with the identical tree
`6bc360281b6ad13ddec5e813f0a00e26b5bc7d6d`. SSH remote inspection was refused by
public-key authentication; a normal HTTPS read of the same configured repository
verified the remote ref. No dependency branch, CI pin or remote was changed.
The earlier WF1 report's local-only dependency statements describe its historical
checkpoint. AC1 does not replay or modify those historical fixtures.

## S0 native owner corrections

SCH-R1 reproduced with Quartz 3.13.1, a fresh owned PostgreSQL 18.6 database and the
real projection hosted service. The native dispatcher first produced an Active
Workflow's completed run, dispatch history and observed admission. Restart then
failed in Quartz's first-fire calculation while the plan remained enabled. An
earlier fixture attempt used a Draft Workflow and was refused before dispatch;
that failed prerequisite remains in the external evidence.

`SchedulerPlanProjection` now makes a bounded native projection decision before
the owner removes a healthy runtime job. It uses the installed trigger's computed
first occurrence, existing run history and admission, without a new schema or
dispatcher protocol. Disabled, ready, exhausted and recovery-pending decisions
are distinct. No-next-fire sets the existing nullable next-fire field; enabled
intent, target/version, authorizer ceiling, history and admission remain intact.
Malformed CRON, unknown time zone, inconsistent receipt, cancellation and database
failures remain observable. No exception message is used as a terminal-state test.

Explicit old starts retain eligible missed occurrences and the three Quartz
misfire instructions. Consumed occurrences advance using Quartz time-zone and
end-bound calculations. A delayed IgnoreMisfire receipt can retain a still-missed
next occurrence; its fingerprint, history, target and current schedule eligibility
are checked before using that cursor. Incompatible changed plans require explicit
reconciliation. An unresolved past admission is never replaced by an invented
fire. Its original native recovery path retains its identity. An implicit start
still means current Quartz time; an already-expired implicit end has no eligible
occurrence. No calendar field exists in the current plan or trigger builder, so
this change does not introduce calendar association support.

WF1-R1 reproduced through both native preview entry paths: the canvas returned A's
run ID after B lost its acknowledgement; the page path already returned no ID.
`WorkflowPreviewOwner` now clears only the active attempt's reservation when a new
attempt starts. Historical accepted A remains available. B can supply an ID only
through its own result or native observation exception. Pending/unknown guards,
authority capture, owner retirement and fresh draft versions remain intact.

Validation used SDK 10.0.303 and configuration `AC1`: 32 Scheduler decision/contract
cases, 80 Workflow component/native-owner cases and 60 Scheduler integration cases
passed with zero failures or skips. The latter include two real replacement hosts
over each retained completed-plan database, explicit/implicit start variants,
future neighbors, unacknowledged admission preservation and invalid replacement
without deleting healthy jobs. The first browser-created finite fire and final
application restarts remain required at A5. The Workflow cases retain the six
native full-document round-trip tests and both consumer entry paths.

The full portability scan included new protected files: 15,206 reviewed findings
unchanged, final enforcement passed without baseline-write mode. Raw attempts,
TRX, exact discovery and source/assembly hashes are retained privately under
`artifacts/agents-completion-ac1`; failed attempts are not relabeled.

## Architecture gate at S0

Pass for the bounded owner fixes. The new internal projection value owns only
Quartz scheduling eligibility; existing native services own database reads,
projection mutations and admission/recovery. Preview state remains in its native
owner. No project references, service locator, new partial class, schema, global
retry or shared rendering dependency was added. Pure decision tests use real
Quartz independently of the host; PostgreSQL tests prove native composition.
CodeAnalytics, Components and dotnetwatch MCP methods are unavailable in this
session; current source, evaluated MSBuild, CLI and Playwright are the fallback.

## AC1 presentation stages

### A1 baseline and current reachability

The maintained [renderer census](agents-renderer-census.csv) starts with 227 current
component entries, including shell contributions, dynamic dialog callers, routes,
descendants and scoped assets. Fourteen retained public or legacy components have
no current product caller. In particular, `ScenarioHarnessPanel` is not made
reachable by the separately active backend ScenarioHarness APIs;
`AgentOverviewUsageList` has test callers only. The current floating contribution
uses `AgentFloatingConversationContent`, not `ContextualAgentWorkspaceWindows`.
The compatibility `/chats` route remains reachable through routing even though it
has no lexical component caller. No dormant public type has been deleted.

The census found the active `AvatarPicker` descendant in both the Agent editor and
Simple Chat definition editor. It belongs to the remaining renderer work; its
upload and generation callbacks require their original editor lifetime. Already
isolated Voice, Floating Settings, provider, definition, conversation and Workflow
surfaces remain the product rendering paths.

Before edits, evaluated restore graphs were captured for 19 protected roots. The
Agents UI closure contains 13 projects and its independent sandbox 16. The source
sandbox was exercised at 1920×1080, scale 1. Three successful existing-page probes
per owned Razor, C# and CSS file were measured: Razor 1652/517/259 ms, C#
350/338/341 ms and CSS 1236/853/955 ms. These are end-to-end edit-to-visible
observations including browser handoff. One earlier C# probe became visible but
its restoration encountered a mapped-file lock; that attempt remains failed and
was followed by byte-exact atomic restoration and three successful probes. A
fresh watch process with warm build outputs reached runtime readiness in 15830 ms.
There is no owned JavaScript in this sandbox/RCL to probe. Final comparative
measurements and published assets remain pending.

### A2 shell and complete Usage family

The actual header/statistics/navigation and explicit defaults confirmation now live
in the existing Agents UI leaf. AgentsHomePage retains exact route tokens, native
tab composition, HR/default-feeding effects and context publication. Shell intents
carry the rendered profile generation and tab; stale commands cannot navigate a
replacement selection. No project reference or application service was added to
the rendering leaf.

All three Usage dialogs now render through the same leaf. Their native public
adapters compose one bounded UsageDetailHost, which owns query equality, frozen
snapshots, error handling and cancellation after reads unwind. Parent lifetime,
profile notifications and authentication-cascade replacement retire the view.
Close works during loading and errors; Retry uses the original resolved interval.
Shared snapshots preserve consumer/provider/model identities and partial, unknown
and unpriced distinctions. Paging and chart construction perform no native read.

The independent `/completion` specimen uses these actual surfaces and production
Parity assets. Large-desktop inspection found and repaired two issues before this
checkpoint: the outer stacks needed explicit stretch alignment, and queued Share
cell templates could read a cleared snapshot while a view retired. All template
values now capture the rendered snapshot, including the denominator. The failed
browser circuit is retained. The repeated two-instance journey passes: closing
the model view leaves the separate provider grid and charts usable; loading Close
and error Retry remain usable with the original accepted window.

Fresh builds passed for the UI, native module and independent sandbox/test owners.
The initial native run passed 101 of 107 cases; six new actor-change harness cases
incorrectly omitted CascadingValue child content on replacement. The corrected
107-case run passed, with no skips. After the browser-derived template repair,
22 independent leaf cases (including both queued-template regressions) and 22
native ownership cases passed on fresh assemblies. Earlier broad topic proof is
retained with its source fingerprint; it is not relabeled as the later source.
The frozen final Stable checkpoint remains pending.

### C# Architecture Gate Result at A2

Status: Pass.

| Severity | Finding | Evidence | Required action |
|---|---|---|---|
| Closed | Deferred cell render could observe a retired snapshot | Failing browser circuit, two retained-template regressions, repeated two-instance browser pass | Keep snapshots captured in render fragments |
| Informational | Native ownership remains outside UI | One per-instance UsageDetailHost and existing AgentsHomePage; no injected services in the new surfaces | Final native consumer campaign remains required |

Dependency direction is unchanged: native hosts consume the existing light UI,
Models/Usage values and shared components. No service bag, locator, new project,
schema or protocol was introduced. The new code-behind is the normal Razor
component pair for a single read lifetime, not a partial runtime-service split.
Independent leaf tests require no product host/database; native ownership tests
exercise real composition. A2 may proceed to runtime/floating completion.

### A3 runtime, floating and active adjunct completion

The eight residual renderer owners now compose actual light surfaces: runtime details,
execution log, context/affinity, floating close choices, projected activity, chat actions,
image attachment input and the complete avatar picker. Native adapters keep policy
redaction, typed stream subscriptions, staging/grants, durable approvals, context bindings
and execution. Runtime/log states are immutable safe values; copying uses the displayed
sanitized message. Context projection captures one binding for both revision and display.

The avatar form uses typed source/generation/validated-upload operations. Each operation
captures its source, request, callback and cancellation scope. Close/reopen and parent
retirement suppress late success and notification. The native Agent editor keys the picker
to its edit session; SimpleChats retains its existing editor-generation key. Native image
validation and paid-provider authority stay outside the renderer. No paid requests ran.

The active Agent switch and thread-history adapters already used neutral renderers, but
their global dialog close was defective under stacking. Two failing-first cases showed
that selecting the first dialog completed the unrelated top dialog. Both now close their
own DialogReference. Stale selection/favorite callbacks retain their original generation;
late favorite readback cannot replace a successor picker. Floating history/close callers
carry their own lifetime through dialog selection. Closing a handle still does not decide
a durable pending approval.

Fresh native production build passed with zero warnings. The final focused native assembly
passed 103 discovered cases, including the existing HostPlatform UTC/poison tests, real
image formatter, standard/floating recovery, activity-stream retirement, stacked dialogs
and new A-B-A context/attachment guards. The independent leaf passed 32 discovered cases
(10 adjunct and 22 shell/Usage). Both runs had zero skips. The first native build failed on
a test dictionary target type and was repaired. The failing-first six-case run retained
three passes, the two product dialog failures and one missing test service registration;
the later pass does not relabel that attempt.

Source Parity browser proof at 1920×1080 scale 1 exercised all seven runtime scenarios,
highlighted original entry, independent views, context changes, close choices and native
InputFile. The actual 645-byte owned JPEG passed through the attachment chooser with
SHA-256 `94C4B46CD9E21BCC89373E6DB3ABDB9EEB2DF66B05DC6E35001FD7B8210530FB`
and decoded at 32×32 in the avatar preview. Bundled avatar assets and both modal actions
rendered correctly; no fresh browser console error occurred. An early scenario assertion
ran before the Blazor state update; it is retained and the corrected helper waits for
accepted rendered state. Browser fixture actions are not native persistence proof.

### C# Architecture Gate Result at A3

Status: Pass for the presentation extraction and bounded native dialog fixes. The existing
UI leaf and sandbox project references are unchanged. New surfaces inject no native
service and do not reference Core/runtime/persistence. Value types retain public
compatibility; typed operation delegates cross only the actual avatar effect boundary.
No generic service bag, schema, admission protocol, copied shared component or new
project was introduced. Existing conversation renderers and native services remain owners.

### A4 preserved Voice, Floating and SimpleChats owners

Voice and Floating Settings remain installation-scoped through the canonical runtime
Workflow database. A selected business database is not their persistence owner. The audit
retains existing provider eligibility, immutable submissions, single-flight controls and
the separate Save, synthesis and playback outcomes. No speech capability was added.

A failing-first native test showed that two Voice settings hosts shared the legacy browser
playback owner. Each host now uses its own existing JavaScript owner identifier and disposes
only that owner's playback after retiring the session. A late synthesis result cannot start
audio. Actual production voice.js was also exercised in an isolated large-desktop browser:
two real Audio objects played independently, retiring one left the other playing, natural
completion released all three created object URLs, and recording permission denial and
unsupported MediaRecorder produced explicit failures. No physical microphone or paid
synthesis was used. Early permission-fixture/CDP errors remain failed setup attempts;
the corrected proof uses an independent browser context with an explicit empty permission
grant. This browser asset proof complements native session/host tests; it does not claim
that the standalone media fixture persisted application settings.

The existing SimpleChat archive and floating-history renderers remain neutral shared
components. Two failing-first native tests reproduced the same stacked-dialog defect as
the Agent adapters: a selection completed an unrelated top dialog. Their native hosts now
close the exact DialogReference, fence queued callbacks across A-B-A parameter changes and
disposal, and bind open dialogs to the contributor lifetime. Retirement closes only its
own dialog, with no archive or focus effect. Domain persistence and operation scopes are
unchanged. The A4 census contained 245 entries, but its classification of the
conversation workspace was incomplete: three inline dialogs remained. The A5 audit
below corrects that finding; A4's passing lifetime tests did not prove that every
reachable form had crossed the presentation boundary.

The native Voice/settings/SimpleChats selection discovered and passed 76 cases with no
skips after fresh production and test builds. Earlier Voice/model/boundary unit proof
passed 50 cases, and the existing JavaScript lifecycle suite passed three. The two product
dialog failures and earlier test-build errors remain separate attempts. The native hosts
retained in SimpleChats are definition/conversation workspaces, profile/authorization
adapters, floating content/contributor and exact dialog hosts; they compose the existing
SimpleChats UI, neutral Conversations and extracted avatar form.

### C# Architecture Gate Result at A4

Status: Pass. Changes are bounded lifetime corrections at existing native ownership
points. No new renderer, dependency, service bag, persistence scope, provider protocol
or implicit business-profile subscription was introduced. Source and published browser
consumer validation remain separate proof layers.

### A5 residual dialogs and complete census

The final form audit corrected the A4 classification of
`LlmChatConversationWorkspace`: its Start and Rename forms and Archive wrapper
were still inline. All three now use `LlmChatConversationDialogSurface` in the
existing SimpleChats UI leaf. The native workspace retains authorization, revision
pinning, persistence, concurrency and operation following. Typed intents capture
the opening generation, dialog kind and conversation identity. Two failing-first
native cases reproduced a queued title callback changing a reopened dialog; both
now pass. Retired callbacks cannot update a replacement opening or disposed view.

The source was prepared and tested in an exact private snapshot while the primary
checkout and Release binaries remained frozen for Stable. After Stable ended,
the six reviewed production, specimen and test files were applied by verified
hash. Four new test clicks and one disposal call were then aligned with the
repository's asynchronous bUnit conventions. All seven owning production/test
projects built, and fresh primary discovery/execution passed 20 native workspace,
five dialog leaf and three source-boundary cases. The initial post-test binary
collector assumed the sandbox's generic output directory; evaluating its actual
Parity/AC1 `TargetPath` repaired that evidence lookup without rerunning tests.

The [census](agents-renderer-census.csv) now records 247 components: 117 isolated
renderers, 57 intentional native hosts, 20 neutral primitives, 39 developer
surfaces and 14 retained legacy components. No remaining product renderer is
classified as an effect host. The native form audit leaves only the catalog
host's bounded failed-load Retry action. Routes, generic dialog callers, dynamic
contributions, descendants and [shared assets](agents-renderer-assets.md) are
explicitly traced. Dormant public types remain intact.

Nineteen evaluated dependency graphs retain their original edges, with no cycle
or unresolved project. Agents UI has 13 projects in its closure and its sandbox
16; SimpleChats UI has five. WorkflowAuthoring UI/sandbox remain 17/19 and the
light Workflows UI shell remains four. No project reference, composition/DI,
schema, persistence, runtime or provider protocol change accompanies the dialogs.
The final C# architecture gate passes for this bounded presentation seam.

### A5 source, assets and development loop

The final candidate image is
`sha256:c58c8923054c23284d9b642efcd536e45fb3c5dc66336d725095f8d37c63da36`,
built from signed `f9dd2b02` plus immutable input fingerprint
`e63526eba98ce8b5e643c035dc419acd9cda4087b234b40fa1a467aadb691839`.
It retains those original labels. A comparison of all 5,508 production,
native-runner and dependency inputs proves equality with the applied source,
with CRLF-only differences recorded explicitly. Only the four intended dialog
production/specimen files differ from frozen `f9dd2b02`.

The three owned apps use the same candidate image and seven matching native DLLs.
Seventeen served assets match their source bytes on all three apps. The image
upgrade recreated only those apps and retained the database, deterministic
upstreams, histories and admissions. Existing 19-case provider protocol, default
catalog and six-case Workflow proof retains its original image/source labels;
its reuse is justified by unchanged protocol, Workflow, build and dependency
inputs. Changed consumers and repeated Scheduler restarts have separate candidate
proof.

Source Parity, independent published Parity and Fast specimens use the actual
renderers, shared Charts, Dialog, CopyButton, font, avatar and media assets. The
new `/simple-chat-dialogs` route passes seven source and seven published scenarios,
including focus, footer reachability, busy/empty states and independent Start and
Archive openings. All new browser contexts use 1920×1080 at scale 1.

Final edit-to-visible samples are Razor 1869/617/462 ms, C# 329/252/272 ms and CSS
1416/805/797 ms. Warm-output runtime readiness changed from 15830 to 17472 ms;
this is not an improvement claim. The added dialog specimen's Razor probes were
1802/456/286 ms, with one unchanged watch process and no full reload. Its Fast
host initially lacked generated shared CSS; the original failure remains and
the actual CSS build repaired the prerequisite. Every probe file was restored
byte-for-byte. There is no owned JavaScript file in these rendering leaves to
probe; actual native voice and shared browser assets have separate lifecycle proof.

### A5 frozen regression disposition

The frozen Release run at `f9dd2b02` executed all 27 Stable assemblies in
3 h 47 m 38 s: **16,489 passed, one failed, zero skipped**. Fresh discovery listed
16,435 cases; 55 additional executions come from seven unchanged nonserializable
theory groups. All 27 owning Release DLLs remained unchanged through completion.
The run remains failed. Its single failure,
`Repository_contains_no_realistic_provider_keys`, found the exact synthetic
security-test key in four retained historical WF1/A2/CA1 evidence files. Each
match was compared to the source negative control and its adjacent test identity;
no historical artifact or scanner rule was changed.

All 3,287 Integration cases passed. The explicit continuity audit identifies 114
editor/capability/team cases and 129 Workspace/Projects/Resources/TestLab owner
cases, all passing. Final dialog proof is the separate fresh 28-case selection
above. The bounded delta introduces none of the current composition, root-build,
persistence or shared Stable-infrastructure invalidation triggers; no automatic
second broad run was launched. The browser fixture repair is outside Stable.
The local serial run overlaps native/browser work and is not a CI timing pass:
current CI uses separate 90/120/180-minute budgets rather than this one local job.

Portability enforcement passes with 15,206 reviewed executable-source findings
unchanged, without baseline-write mode. The five static/documentation/package
tooling families passed 46 self-tests. Source and task-delta secret scanning and
the broader private-artifact scan are reported separately; the full worktree is
not declared secret-free.

### A5 native campaign closure

Candidate native SimpleChat create/send/rename/archive/reload, all shell tabs and
Voice/Floating settings persistence, source-and-two-client custom metadata,
two-circuit local-setting conflicts and source disable/retire/reimport have
passed. Scheduler proof created a finite enabled plan in the native UI, observed
its real run, restarted all three apps twice without changing plan/run/history/
admission identities, and observed a separate future fire exactly once.

The final floating journey passed all original 60-second assertions in 5 m 56 s:
detach/follow and next-turn context, same-session adoption, independent handles,
Stop preserving a durable pending approval, history reopen and explicit rejection
on that original run. Agent configurations were unchanged. Final standard chat
also passed: the saved nondefault opaque model, original prompt/answer and session
survive reopen; runtime/log details target the original run, and cancellation
terminates the separate pending run without a file write.

The final file journey passed hidden-canary reading, two exact native write and
attachment approvals, explicit rejection, unchanged sibling bytes and native
readback/download of the 57-byte result. Final media proof passed real generation,
attachment, authorized preview/download and vision of the same 68-byte PNG. Only
the external model responses were scripted; native effects, approvals and bytes
were real. No paid provider request ran.

Final History proof passed lazy query, global/scoped exact attempt identities,
distinct caller keys, denied content, committed credential deletion, HTTP 401 for
the revoked key and a new native turn under the replacement. The original client
source secret was restored. Canonical Agent, Workflow and SimpleChat content has
separate owner authorization proof; metadata access does not grant content access.

The Usage audit first preserved a partial native snapshot: four SimpleChat
observations were visible while Agent indexing was incomplete. The existing
resumable maintenance executable initialized only the owned client's derived
index, without rebuild/migration flags or new calls. All 945 canonical JSON
payloads remained byte-for-byte unchanged. Its read-only inventory initially hit
Windows long-path handling; extended-length reads repaired that helper before
any maintenance effect.

After the final calls, 56 canonical Agent observations plus four SimpleChat
observations agree with the API and all three native dialog metrics: 60 total,
58 known, two unknown, zero unpriced, 1,102 tokens and $0.005887 known cost. The
dialogs display $0.0059 using their existing rounding. Nonzero unpriced rendering
retains focused owner/leaf proof; this native fixture does not invent an unpriced
charge. The accepted seven-day UTC interval stays pinned across all details. An
independent fourteen-day Agent view cannot change or close the first view. Native
reads on the source and second client retain explicit partial coverage and contain
none of the first client's consumer identities. No browser circuit error occurred.

An earlier floating rejection exceeded the unchanged
60-second browser assertion while Stable was running. Fixture disposal then
cleared its still-needed response plan, so the original continuation received
the deterministic default response and eventually failed. The failing attempt
and exact rejection/run identities remain. A failing-first fixture test now
proves that disposal retains an unconfirmed response plan; no approval was
replayed and no timeout was increased.

The first final-image standard turn was refused after its owned source credential
expired at 01:14:34 UTC, three seconds before the prompt. Its original run is
terminal Failed, with no approvals or receipts and zero response-plan consumption.
The operator renewed only the existing secret on both owned clients through the
native API Access/Secret vault UI, retaining subject, scopes and the 240-minute
lifetime. Both source connection tests and secret readbacks passed. The unused
response plan was explicitly retired after inspection; no original turn or setup
was replayed. This prerequisite failure remains separate from the fresh verification.
The fresh standard verification passed after that renewal. All required native
journeys are complete, with original failed attempts and their disposition retained.

### Delivery and qualification

The main implementation checkpoints are `44730fc9` (S0), `f62d6c8f` (shell/Usage),
`ccc53578` (runtime/floating), `f9dd2b02` (preserved integrations and frozen Stable)
and `00dada9f` (residual dialogs and consumer fixture retention). The sixth
checkpoint closes the census, documentation and final evidence. Each is signed
and verified with OpenPGP fingerprint
`96E836FAA8854EE98ABC10903C206549E1D7EAD6`; exact SHAs, source fingerprints,
artifact hashes, TRX counts and signature receipts remain in the private external
AC1 evidence ledger. Delivery is local: no push, merge or release was performed.

Final source secret scanning covers 6,710 text files and one explicit binary
exclusion. All 306 findings on 111 paths match the reviewed entry content (four
paths differ only by CRLF); no new source secret finding is introduced. The final
changed-file scan is separately recorded. The earlier full-worktree scan remains
failed over retained private/historical artifacts and synthetic controls; its
coverage and exclusions are retained, and it is not called clean.

All new browser contexts use 1920×1080 at scale 1. The final PNG inventory
distinguishes 181 viewport captures, six full-page captures at the same desktop
width and two actual one-pixel generated content files. Owned watch/sandbox
processes and the seven exact native/Stable containers are stopped after proof;
their data, containers and private logs are retained. Historical fixtures and
port 5032 remain untouched. Workbench is the next extraction family, followed
by its cross-owner Structure work and then Processes; none was started in AC1.
