# Provider Sharing PP2

Provider Sharing PP2 is complete, including PP2C-R1, Sharing/source/refresh rendering,
the independent sandbox and the final rebuilt-image campaign. S0's 2026-10-02 prerequisite
is preserved. Qualified frozen Stable results and retained log limitations are recorded below.
The completed [PP1 boundary](provider-profiles-ui-pp1.md), A2 and historical bundles are retained.
This record does not claim all provider UI is extracted or that the application is release-ready.

## S0 changes and ownership

Provider tree descriptions now resolve the source model display name while keeping opaque route
IDs in native requests and persistence. Provider catalog reads retain the canonical configuration
revision from the existing snapshot loader. A changed imported editor is acquired and checked
against that same revision before the catalog/editor pair is accepted. Unchanged imports keep
the same-target no-op; dirty local drafts, raw invalid fields and price-row contexts survive.
Late reads, A-B-A selection and inconsistent revisions are explicitly fenced or unavailable.

The Sharing slot is keyed to provider identity rather than the PP1 editor context. Its separate
alias draft survives a remote metadata refresh; the Sharing read retains existing state during
refresh and refuses writes after a failed read. These are narrow lifetime repairs, not changes
to publication, import, persistence or source authority.

The native relay now uses the existing canonical provider-configuration projection. A real
save/publish/relay regression reproduced the old failure: persisted connector metadata represented
the timeout as text, while the native driver expected the canonical numeric configuration.
Failure logs include the exception type with request/publication identity, without exception
messages that could contain private configuration.

The E2E runner accepts an owned fixture name, isolated ports and explicit private network ranges.
Reset requires the exact root marker and matching Compose ownership receipt. All non-ingress
networks remain internal. Permission setup runs before applications; subsequent commands use the
explicitly provisioned services and require the helper to remain successfully stopped. This fixes
a reproduced recursive ownership-change race with an active workspace probe. Reverse-order
unpause cleanup uses valid PowerShell iteration and retains bounded failure handling.

Native test oracles were corrected against the actual owner contracts: successful authoritative
catalog absence means `Missing`; Responses streaming requires a final completed event, not the
Chat Completions terminator. Fixture client credentials include the existing execution permission
needed by the real test-chat endpoint. Catalog sanitization continues to reject private fields
and credentials while allowing deliberately published model names.

## S0 provenance and proof

Entry was bundle-only commit `df9b7c8e1b563d56fef762c38aad2a14089bd332` on the existing branch;
the preceding PP1 product commit is `00c395ba0f4ce62c611c62718d8a510f9044f7b4`.
Components source is `4a858412d2c2a3f6123bf23d8c4584f05b47627d`; FileTools source is
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`. Neither sibling was edited. Host SDK is 10.0.303;
the Docker build uses the repository's 10.0.302 SDK / 10.0.10 runtime pins. Host proof uses the
isolated `ProviderSharingPP2Proof` configuration.

The successful S0 image contains the recorded entry plus its implementation worktree delta,
fingerprint `add9e002f6c32eace25ce83934e35b940622f724b62816f7b29c984eec905ced`.
All three applications reused image
`sha256:60cdd5b711958ac230a27c9f902be32fb5ac1e1c93fe49b6bca19b5f33dce4e1`.
The separate deterministic upstreams reused
`sha256:4b5fed3ec1eba8770348fbe48467ff418e0aca488c157a5228b754d35d15991b`.
Source files were frozen throughout the S0 runner. Its image is historical; the final
post-extraction image and its signed production source are recorded below.

Private evidence lives under `artifacts/provider-sharing-pp2/20261002-df9b7c8`; credentials and
raw runtime material remain in the distinct marked `.artifacts` fixture. Failed attempts are
preserved and are not counted as successful whole runs.

| Selection | Discovered / passed | Evidence |
|---|---:|---|
| Native relay composition and Responses terminal controls | 9 / 9 | `s0-native-terminal.trx` |
| Authoritative absence and identity-preserving reappearance | 2 / 2 | `s0-absence.trx` |
| PP1 seam and imported refresh component cases | 24 / 24 | `s0-components-after.trx` |
| Snapshot races, reads, session and reconciliation | 62 / 62 | `s0-unit-after.trx` |
| Existing Sharing/source/recovery/delivery component consumers | 39 / 39 | `sharing-owners-before.trx` |
| Native fresh three-instance browser lane | 1 / 1 | `native-defaults-ui-06.trx` |
| Full network-separated protocol scenarios | 19 / 19 | `runner-s0-08.txt`, `s0-08-scenario-results.json` |
| Guarded preparation / refusal controls | 6 / 6 | `fixture-isolation-02.txt` |

The browser lane uses 1920x1080 at 100% scale, four independent circuits and actual production
publication, vault-reference, source discovery/import and synchronization controls. It captures
as-delivered accepted catalogs before customization: OpenAI Responses (10 models), OpenAI Chat
Completions (10), OpenAI Image (3), and the native Ollama seed (5). The runtime-only Ollama seed
is refused publication; a saved native profile retains its accepted definitions. Both clients
retain exact names, defaults, prices, Thinking and model constraints. Twelve default/non-default
chat invocations independently match client routes to exact upstream model tokens. A second
client-A circuit stays old until toolbar refresh after source default/add/remove changes;
client B stays old until its own synchronization. Screenshots were inspected at the desktop size.

The 19-case protocol vector includes duplicate names, caller-scope separation, tool/structured
and image transports, ETag/304, streaming cancellation, unpublish/reappearance, identity mismatch,
restart/outage/recovery, personal-provider no-fallback and secret/content audit redaction.
These are deterministic native network tests, not paid external-service evidence.

Complete portability scan 07 covers 8,077 files and 33,692 findings, including new protected
files. The reviewed baseline delta is eight added and three stale findings: HTTP header case,
regex escaping, guarded fixture root/name paths, and a false-positive `CmdletBinding` elevation
match. No scanner rules changed. Final no-write enforcement passes with 15,252 reviewed
allowances. Documentation and secret-artifact checks also passed; the latter included oversized
reports, with no unreadable or omitted text files. The image permission helper retained its exact
identity/start time and successful exit throughout runner 08.

## Completion execution

### Completion entry and PP2C-R1

The completion package entered at `eaa7404b6c16b7db6d75668b5b4dfa2a04a6ac9c`, preserving
signed S0 `9052b2a443a4fdce1ac24dd0ccfce236fd35ab74`. PP2C-R1 was reproduced through the
actual PP1 toolbar and Sharing child, with two native management owners and PostgreSQL:
after B saved `Operations model` and disabled the import, A refreshed and saved the older
`Team model` value. Native old-token refusal was independently verified.

The Sharing draft now owns a distinct local baseline and token pair. A clean draft adopts
fresh local settings. Dirty text survives metadata-only refresh when the accepted local
settings still match its baseline. A concurrent local change retains the old baseline and
shows explicit use-saved/keep-edited-fields choices; unchanged fields adopt the reviewed
saved values. Save captures an immutable submission, and confirmed read-back merges accepted
values by field revision, preserving later typing and the same EditContext. Native concurrency,
commit recording and acknowledged recovery remain with their existing owners.

Evidence is in `artifacts/provider-sharing-pp2-completion/20261002-eaa7404`. The original
failure is `r1-before-02.trx`. The focused current-source selection discovered 55 cases:
`r1-after-02.trx` passed 54, with one test incorrectly expecting immediate unlock after a
parent refresh retired a delivery. The corrected assertion explicitly retries that original,
already acknowledged delivery; `r1-delivery-after.trx` passes its one freshly discovered case,
proving no repeated write and preservation of later input. Together these cover ten native
local-concurrency cases and 45 retained Sharing/source/recovery/PP1 component cases. Earlier
binding, fixture and compilation failures are retained separately. Production and owning test
assemblies were rebuilt before discovery in `ProviderSharingPP2Proof`.

The final-image two-circuit repetition passes in `final-native-consumers-11.trx`. The signed R1 checkpoint
is `bfa934b26af61794118f249c31bc47da65a07ee3`, verified against the configured OpenPGP fingerprint
`96E836FAA8854EE98ABC10903C206549E1D7EAD6` in the retained native session. This bounded repair
does not close structural PP2 or its consumer campaign.

### Sharing renderer checkpoint

`CanDoItAll.AgentFramework.SharedProviders.UI` now contains the actual Sharing surface, local
publication child, imported-profile child, local draft and confirmation rendering. Its references
are BaseLib, Components.Web and the existing neutral SharedProviders abstractions. The native
module maps safe presentation values and retains its management calls, recovery ledger, commit
recording and acknowledged parent delivery. Existing PP1, A2 and their leaf graphs are unchanged.

Rendered intents retain the view, activation, accepted snapshot and exact provider/publication/
import/source identities and versions. Handler admission rejects stale callbacks, A-B-A returns,
ineligible publication and replaced confirmations; an old cancel cannot close a newer confirmation.
The imported facts use BaseLib FactTable/Grid, avoiding the module's inherited ReadOnlyFact helper.
Sharing remains an inline editor; confirmation is compact, and the parent editor keeps scroll ownership.

`sharing-leaf.trx` passes all 15 discovered independent renderer/graph/signature cases, including
transitive forbidden references, missing edges and cycles. `sharing-native.trx` passes all 59
discovered native host/PP1/recovery cases, including the ten PostgreSQL local-concurrency cases
after the actual child move and four new rendered-origin cases. The leaf and production host build
without warnings or errors; the owning native test assembly retains 15 unrelated existing warnings.
The complete proposed-source static scan passes with 15,252 unchanged reviewed findings.

Architecture review permitted the Sources/refresh work to proceed: actual rendering has moved,
the host is an effect owner, no runtime contracts or authority changed, and pure tests need no
native module. The later sandbox and final-image sections record the combined surface proof.

### Sources, discovery and refresh checkpoint

The leaf now also renders the source list/editor, metadata-only credential choices, discovery
catalog, enablement/delete confirmation and reusable refresh control. Sources is a wide dialog,
its editor is medium, and confirmations are compact. Catalog scrolling is bounded to 52vh with
the action footer outside the scroll region. Large-desktop inspection found and repaired intrinsic
list sizing and Enter-to-save: an internal submit control reaches the same guarded handler as
the visible footer. Refresh has intrinsic width (183.5px in the recorded 1920×1080 sandbox).

Source drafts retain proposed/native identity, expected version, validation context and raw
input. Save snapshots immutable values and field revisions before awaiting the owner. Canonical
read-back normalizes unchanged fields and preserves later typing. A late result cannot close or
write an error into a replacement editor. Missing credential metadata/references remain explicit;
only identifiers and labels cross the renderer boundary. Delete confirmations retain the displayed
source/version and unique confirmation identity.

Native catalog tests reproduced stale dialog origins after failed read-back, delivery and lost
acknowledgement (`catalog-recovery-before.trx`, three failures). The host now retains the original
catalog submission until canonical reconciliation. It updates only that dialog's accepted origin,
preserves later checkbox edits and performs no repeat synchronization during verification/delivery.
A different canonical selection retains the draft with an explicit review requirement.

`sources-native-final.trx` passes 32 of 33 selected cases, including all ten new PostgreSQL source
and catalog cases. The remaining older test used an ambiguous button-text selector after the Enter
fix added the hidden submit control; its corrected exact control selection passes in
`source-selector-after.trx` (1/1). The earlier source selection passed 30/30 before the catalog
recovery extension. Failed attempts are separate from the passing follow-up.

The independent sandbox has 26 scenarios and a stored fixture separate from each editor draft.
It includes local/imported/runtime-only, loading/failure, metadata/credential failures, retired/
unavailable states, held/rejected/unknown writes, failed read-back/delivery, deliberate verified
retry and 200 publications. Fifty-one independent renderer/state/closure tests pass in
`sandbox-desktop-final.trx`; no database, module or external HTTP registration is involved. The evaluated
graph is exactly five projects: sandbox, leaf, SharedProviders.Abstractions, BaseLib and Common.

Source and independent Production-published browser scripts use 1920×1080 at scale 1, real controls,
hit-tested footer geometry, keyboard Enter/Space/Escape, nested dialogs, two-editor conflict/merge,
bounded catalog scroll, fonts and BaseLib interop. Their console/request error collections are empty.
The final layout receipts are `sandbox-source-final.json` and `sandbox-published-final.json`
under the completion evidence root. Both include the full-width catalog correction.

The Sources architecture gate permits final-image validation: the actual markup and effects
are separated, the sandbox graph is independent, canonical recovery preserves the original
operation and native optimistic checks remain authoritative. Documentation validation passes
345 maintained files. This stage does not close the final-image or native-consumer gates.

The clean five-project sandbox did not reproduce PP1's second Razor-update failure. In
`ProviderSharingWatchProof`, SDK 10.0.303, polling `dotnet watch` applied three Razor, three C# and
three scoped-CSS probes with the same server PID. Visible latency was 0.49–2.32s for Razor,
0.77–0.78s for C# and 0.47–0.89s for CSS. The first C# probe method clicked before compilation;
those three timeouts are retained separately, and corrected bounded action polling observes the
changed method. Startup build was 4.82s, graph load 1.3s, navigation 388ms and hydration 607ms.
All three source hashes were restored. This is current-sandbox hot-reload proof, not a claimed
universal speedup or a rewrite of the historical PP1 restart measurements.

Management stays in
ProviderManagement; mutation recovery, target verification and acknowledged parent delivery stay
in their original AgentFramework service owners. Preserve request History and source consumers
through their current host slots. CodeAnalytics, Components and dotnetwatch MCPs were unavailable;
source inspection, native CLI and evaluated graph/browser proof are the fallback.

At this checkpoint the remaining work was the final rebuilt-image protocol/UI/consumer campaign
and requested frozen Stable checkpoint. Their results follow. The task stays within PP2, uses
zero paid calls and does not push, merge or begin another family.

## Consumer follow-up before the final image

The two-circuit import journey reproduced a committed write whose parent revision refresh
cancelled its own delivery acknowledgement. Same-provider reads now wait until the active
operation finishes; switching providers still retires that operation. The failing-first receipt
is `ack-regression-before.trx`; the lifetime/recovery follow-up passes 26/26 in `ack-followup.trx`.

Workflow's existing provider option projection omitted shared model names and ownership.
The native browser displayed opaque route IDs and permitted a model override. Safe catalog
metadata now travels through that existing projection to the existing selector. The two
catalog implementations retain exact IDs, and the selector preserves published saved choices,
explicit unavailable choices and source-managed override refusal. No provider authority,
persistence schema, registration or dispatch policy changes. Direct Models, Workflow Core,
Components, AgentFramework and Web builds pass; focused selector/acknowledgement tests pass
14/14 and Workflow catalog tests pass 23/23. The native canvas also correctly refused a test
model outside its immutable Prompt Gallery component's supported set; the consumer fixture
must bind that component to the intended model instead of weakening compatibility checks.

These are bounded consumer follow-ups to the frozen Stable outputs, with explicit owning-family
tests and renewed container/browser validation. They do not change shared persistence, DI,
root build configuration or test infrastructure. Earlier image receipts remain stage evidence:
the acknowledgement and Workflow projection fixes require a new application image and repeated
19-scenario, native-default and consumer campaign before final closure.

### Final image and native fixture ownership

Runner 05 rebuilt the final application from `ddba548adf7eda1e5e07bf6aa69428320aa7bee4`
plus the recorded consumer harness. Application image
`sha256:2dcab864af44102998dfc08265badb883194bf8ce36b4e211f70228cc876f94e`
and deterministic upstream image
`sha256:a996db3d5cf31890794a36e6f9bcc81fb3e19a940714a0afb135035f0492c91d`
passed all 19 protocol scenarios. `final-source-inputs05.json` records every source input;
`final-image-source-equivalence.json` identifies subsequent test/documentation changes and their hashes.
No production, upstream, runner, configuration or dependency input changed after this build.

The native-default fixture now captures untouched bootstrap presets, creates independent
operator-owned profiles through the actual editor, and verifies that all seed identities and
metadata remain unchanged. Earlier stage 10 renamed a reserved seed and reproduced the native
bootstrap ownership refusal on restart. That failure remains in `stage-fixture04` and
`stage-native-consumers-10`; the correction is fixture ownership, not a change to bootstrap
authority. Resetting catalog prices is also not a copy operation: the preset retains older
price rows. The fixture therefore copies its exact price values through the native price editor.

`final-native-defaults-05.trx` passes its freshly discovered case on both clients, including
OpenAI Responses/Chat/Image, saved native Ollama, runtime-only publication refusal, complete
catalog/price/Thinking parity and separate-circuit refresh. The independent wire oracle verifies
exactly 12 upstream calls across six opaque routes with the expected source model names.
Earlier setup/readiness/price assertion failures remain separate. The native consumer
journeys are reconciled by exact test identity in `final-native-consumers-reconciliation.json`.

### Frozen Stable reconciliation

The requested checkpoint froze `6a266e6686264076f5ff1c7c98aa06b4439b7010` outputs in
`ProviderSharingPP2Stable`; later builds used the separate proof configuration. All 24 assemblies
executed, with 16,104 discovered rows expanding to 16,159 runtime cases. The original result is
16,144 passed, 15 failed, zero skipped. `stable/final-reconciliation.json` records each output
hash, actual count, theory expansion and original failure. This is a qualified checkpoint, not
an unfiltered green result.

Ten Integration failures occurred when the owned one-gigabyte PostgreSQL tmpfs filled. The
same frozen assembly passed all 102 selected History/Workflow delivery cases on a separate owned
PostgreSQL 18.6 instance with eight gigabytes of tmpfs. The exact ten failed rows are included in
that passing follow-up. Original database logs and the first unreachable-network retry remain
separate; no ordinary database or retained application was used.

Three Unit guards matched `Providers.UI` as if it were the forbidden `Providers` runtime
project. Their assertions now name the runtime `.csproj` explicitly. Host classification also
identified three native tests using secret protection without `HostPlatform`; those class tags
are now present. The four original failing checks pass in `stable-guard-followup.trx`. These
test-only corrections require owning follow-ups, not another whole Stable sweep or image rebuild.

The source-secret check read actively written logs and synthetic key samples exported in
security-theory names, including two historical A2 reports. The historical reports remain
byte-for-byte unchanged and every match was verified as the generated security fixture. The
identical frozen scanner passes against a hash-verified copy of all 8,276 current source inputs.
That source-only follow-up is qualified separately from the original checkout scan; it does not
claim to have scanned ignored historical artifacts. Current exported discovery/TRX samples are
redacted with exact originals and hashes retained privately. Safe artifact-export scanning is a
separate closure check, and scanner rules are unchanged.

### Native consumer reconciliation

The initial nine-case run is `final-native-consumers-11.trx`. Six cases passed, and its three
failures remain visible alongside exact follow-ups. All executions use the same final05
application/upstream images, separate owned stores and 1920×1080 at 100% scale.

| Journey | Passing receipt | Native evidence |
|---|---|---|
| Custom catalog, default, prices, Thinking, restart and missing saved choice | `final-native-consumers-11.trx` | Independent source/client metadata; saved Agent/Simple Chat/Workflow choices and explicit refusal |
| Two-circuit local import conflict and explicit merge | `final-native-consumers-11.trx` | Old draft cannot overwrite another operator; reviewed merge and original state restoration |
| Disable, HTTP-policy refusal, retire and reimport | `final-native-consumers-11.trx` | Native refused dispatch, original import identity and restored source |
| Simple Chat default/non-default selection | `final-native-consumers-11.trx` | UI save/reopen, native completed message/transcript and exact upstream model |
| Accepted Workflow and TestLab | `final-native-consumers-11.trx` | Matching immutable input/version, completed output and stored native artifact |
| Incomplete Workflow response | `final-native-consumers-11.trx` | HTTP-success response refused by output validation; no accepted artifact |
| Lazy History and credential rotation | `final-native-consumers-12.trx` | Identity-keyed reads, pagination, public denial, revoked 401 and successful rotated credential |
| Agent/Project Structure file journey | `final-native-consumers-15.trx` | Hidden canary, exact approvals, explicit denial, native create/attach/read-back, preview and browser download |
| Image generation, attachment and vision | `final-native-consumers-18.trx` | Actual PNG, stable native identity/hash, loaded preview, authorized content download, UI attachment and exact decoded upstream bytes/model |

The file journey matches all nine upstream requests to the exact source model, preserves
unrelated content, and compares the stored/downloaded bytes and hash. Approval evidence is saved
before each decision with a unique native run identity. All nine distinct journeys pass after
the recorded focused follow-ups; the initial nine-case result is not described as green.

Bounded browser corrections retain their failed runs: use the public safe History denial,
allow a native approved effect to finish within the eight-minute test deadline, avoid reading
the expensive native owner while no undecided approval is visible, select the actual stored
download filename, open the authorized image preview, wait for the attachment input to become
enabled, and decode complete JSON string tokens inside a truthfully truncated capture selected
by the current run's unique marker. The vision PNG contains a base64 `+` serialized as
`\u002B`; raw JSON substring comparison was an incorrect oracle. Neither capture limits nor
production timeouts were changed.

Project Files remains text-file-only. Image bytes are downloaded through the existing authorized
Project Structure content API after proving the actual image preview loaded. Those downloaded
bytes are attached through the real chat UI and sent through the supported vision path. The file
journey separately proves the browser download action. The fixture serves a valid 68-byte PNG,
not a three-byte transport placeholder; all model responses remain deterministic local fixtures.

The later host-classification selections pass five Integration cases and twenty Component cases
(nineteen initially, then one corrected acknowledged-delivery expectation). Current source-only
secret scanning uses the identical frozen Unit assembly. These exact follow-ups account for the
original Stable failures and post-checkpoint changes; no second unfiltered Stable sweep is claimed.

### Log and delivery qualifications

`final-server-log-origin.json` records the actual final container IDs, image digests, PIDs,
start times, working directories and log hashes. `final-server-log-review.json` accounts for
protocol/permission denials, incomplete Workflow output and the saved-unavailable refusal.
The required browser error collections are empty. Server logs are not represented as clean:
unchanged framework mapping/query warnings and background History maintenance budget retries
remain visible. Two tooltip-disposal cancellations from detached circuits occurred around failed
attempts (02:06:37 and 02:55:52 UTC), outside the passing file/image journey intervals;
the unchanged sibling catches disconnect/disposal exceptions but not this canceled interop call.
A pre-campaign undecryptable antiforgery cookie was rejected; its original caller is unestablished,
and it did not recur during the passing native-default/authentication campaigns. These bounded
observations do not establish an application-wide performance or lifecycle release gate.

The final no-write portability gate retains 15,254 reviewed allowances, including two reviewed
FileTools namespace false positives in the native evidence helper. Scanner rules are unchanged.
`final-portability-scan06.json`, `final-portability-no-write07.txt`, `final-docs03.txt` and
`final-safe-export.json` are the final complete-source, documentation and strict export receipts.
The export scan includes oversized reports and exact text copies of script extensions outside
the scanner's default suffix list; generated published build trees and binary images are separate
build/visual/content proof. There are no secret findings or unreadable/oversized text omissions.

Six coherent local checkpoints cover R1, Sharing, Sources/sandbox, the bounded consumer fixes,
Stable guard/classification repairs and final validation. `all-delivery-signatures.json` records
each SHA and native OpenPGP verification against fingerprint
`96E836FAA8854EE98ABC10903C206549E1D7EAD6`; `final-validation-commit.json` identifies the final
checkpoint without embedding a self-referential commit ID here. The working tree and both source
siblings are clean, and `final-image-source-equivalence.json` proves the final image inputs still
match executable production/upstream/runner sources.

`final-owned-cleanup.json` records shutdown of the six completion fixture services and its
separate capacity-retry database by verified container ID. Persistent fixture data and all evidence
are retained; stopping the disposable retry database releases its temporary in-memory data.
Source/published/watch sandbox hosts are stopped. S0, the earlier test database, ordinary hosts
and the retained real-provider pair are preserved. The completion package and historical bundles
are unchanged; `evidence.json` and the package's final consistency check close all 39 PP2 groups.
No paid call, push, merge or later provider family was performed.
