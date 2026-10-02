# Provider Sharing PP2

S0's container model-name/default/routing/security prerequisite passed on 2026-10-02.
Sharing/source/refresh rendering and the independent sandbox are extracted. The final rebuilt-image
consumer campaign and frozen Stable checkpoint remain open.
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
Source files were frozen throughout the complete runner. This documentation is a subsequent
non-executable addition; the final post-extraction image must be rebuilt from signed sources.

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

## Remaining PP2 work

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

The final-image two-circuit repetition remains required after extraction. The signed R1 checkpoint
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

Architecture review permits the Sources/refresh work to proceed: actual rendering has moved,
the host is an effect owner, no runtime contracts or authority changed, and pure tests need no
native module. Browser geometry, full sandbox closure and final-image consumers remain open and
will validate the completed combined surface before PP2 is declared complete.

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

Outstanding closure includes native custom prices/Thinking and saved-unavailable choices, image/vision bytes, credential rotation,
actual Agent/Project Structure file approvals, Simple Chat transcript, Workflow/TestLab outcomes,
retained History and neighboring boundaries. Repeat the protocol/UI/consumer campaign on final
rebuilt images and run one final frozen Stable checkpoint.
Verify all coherent OpenPGP commits and clean only task-owned resources. Zero new paid calls,
no push/merge, and no additional family are authorized.

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
