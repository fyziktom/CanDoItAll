# Workspace Settings Core — current-source review and design constraints

All implementation source references below resolve to the review SHA in [SOURCES.md](SOURCES.md).
This is a source review and proposed extraction design. Product builds, live UI behavior and
fault reproductions have not been executed by the reviewer.

## 1. Why a slice, not the whole Workspace module

The Settings route currently combines four modest operator/editor surfaces with much larger
Data Sources and Storage/recovery trees and separate API token/user administration (WS01,
WS02, WS15). Those areas have different persistence and security owners. Completing all of
Workspace in one move would contradict the preference for smaller steps before the largest
modules. The chosen unit includes the real shell and four complete sections, not one demo
component. See [MODULE_SELECTION.md](MODULE_SELECTION.md) for what remains.

The Settings route has **eight navigation entries**, including the Providers redirect; there
are seven local sections. Preserve the exact tokens and the redirect rather than treating
all eight as local tabs or dropping deferred entries from production.

## 2. Existing dependency pressure

The module's project references Infrastructure, Projects, Security, AppComponents,
Configuration.UI, AgentFramework.Models and ProviderHistory.Abstractions (WS03). Moving the
Razor file into a new project while retaining these module references would not establish a
small sandbox. The actually needed primitive/presentation types must be separated from the
implementation references that happen to contain them today.

`WorkspaceSettingsModel` is a plain mutable six-field model in the same source file as EF
entity/mapping and a broad partial service (WS04). `WorkspaceProviderOption` plus its list
interface are small values/port (WS14). These are candidates for a small Contracts project.
Retain namespace/wire/default compatibility for actual consumers; the HTTP endpoint uses this
model for response/schema metadata (WS13).

Security's `SecretListItem`, `SecretEditorModel` and enum also share a file with EF/vault
operations (WS05). Do not relocate the Security implementation into Workspace or make a lower
Security owner reference the Workspace implementation. A narrow Security data seam or UI
projection is acceptable; avoid duplicating a second secret vault contract for every tab.

File preference types live in Infrastructure alongside file coordination/migration code (WS07).
Project only what rendering requires, keeping normalization/validation authoritative at the
existing owner. The new feature need not relocate the machine control plane. History already
has a lightweight public port and immutable values (WS10); reuse them instead of copying its
protocol into a Workspace-specific equivalent.

## 3. Shell and loading

`SettingsPage.LoadAsync` loads defaults, provider options, secret metadata and API status
sequentially, then reapplies the requested tab (WS02). This unnecessarily entangles independent
sections and creates opportunities for late initialization to interfere with current input or
navigation. The new host should own route state, while each section owns only its required
reads and editor lifetime. Metadata statistics may be independently loaded, but plaintext,
history policy and deferred administration must not be prefetched just for a header.

A view slot is a composition boundary, not a covert backend edge. Data Sources, Storage and
API Access remain their real production components rendered only when active. The sandbox
must not claim those implementations are extracted. A loading/unavailable API status is not
proof of anonymous API configuration. Providers stays a host navigation intent.

## 4. Defaults: preserve normalized results and the durable boundary

The current Save handler passes its live `settingsModel`, ignores the normalized result,
then refreshes provider options. If the latter fails, the catch reports a save failure even
though the settings may already be committed (WS02, WS04). A source-level immutable capture
and an outcome-aware follow-up fix are part of the extraction, not a new backend.

`SaveSettingsAsync` finds the latest row by UpdatedAtUtc, normalizes defaults/currency and
updates CurrencyDisplayState before SaveChanges. A failing write can therefore publish a
currency state not backed by persisted settings. Add a fault test and make any correction
at this owner boundary small and explicit. A read also updates normalized currency display;
do not mistakenly promise all existing reads have no in-memory publication side effects.
Activity is post-save; its catch logs and can itself fail. Known committed facts must survive
secondary faults without hiding real persistence/acknowledgement uncertainty.

The HTTP API performs stricter validation than the UI owner, rejects unknown fields and checks
an enabled default provider. It has read/write scopes and returns saved values with a pending
read-back header on its defined secondary failure path (WS13). Preserve that distinct transport
contract. This bundle is not authorization-policy unification.

## 5. Secrets: current paths and bounded corrections

The page's edit request has no origin fence. Save/Delete refresh the list and reset the shared
editor after awaits; the returned create ID is not adopted first (WS02). A late result can
replace or clear the successor, and a refresh failure can leave a committed create looking
like an unsaved draft. Source inspection establishes these risks; Codex must reproduce them
with held owner calls before changing behavior.

The real owner intentionally separates metadata and payload (WS05):

```text
Save: validate → find/create metadata identity → stage new vault payload
      → save metadata reference → delete superseded payload → Activity → return ID
Delete: reference checks + mutation coordination → metadata transaction commit
        → delete vault payload → Activity
```

This is not a single atomic transaction across PostgreSQL and a vault. Do not claim that a
view cancellation rolled back those effects. Redacted stage facts from the real boundary
can preserve the confirmed ID and cleanup warning. Unknown acknowledgement must remain
unknown; a read can observe exact current state but not prove the earlier request's origin.
Examine the existing staged-payload cleanup behavior before modifying it. Do not fix a UI
message by deleting a possibly referenced payload, swallowing every error, disabling reference
policies or changing all public upsert semantics.

Keep sensitive payloads out of generic receipts. A pattern suitable for Resource metadata
cannot be blindly reused for a full secret command. Store private transient submission state
only as needed, with no serialization/history/logging exposure. New tests must use private
fixture vaults and synthetic values. Preserve explicit-load, SecretField, masking/copy/timer,
legacy decryption and reference-only list semantics. Details are in [SENSITIVE_STATE.md](SENSITIVE_STATE.md).

## 6. Files: a different origin and an existing identity

The panel has Save/Delete busy admission, but its list selection can still change while an
operation awaits. Save later assigns the captured extension to current selection; Delete
later resets the shared editor. Independent selection/draft origins are needed even though
this service is host-local (WS06).

The owner normalizes extensions and executable paths, writes under coordination, and logs
afterward. List may encounter stored rebind-required paths or perform the existing lazy
legacy migration (WS07, partially read). Preserve those meanings. Read the remainder of path
validation/durable-write helpers before implementing outcome handling. A failed log after a
known write is not proof of no write. Do not introduce local execution, shell templates,
path migration UI or automatic activation of foreign-host paths.

## 7. Provider history: preserve the mature safeguards

This panel already has useful separation of policy values, explicit loading, expected version,
confirmation and auth/profile cancellation (WS08–WS12). Preserve and relocate the good parts,
not rewrite the policy system. Opening this tab must cause no policy/history read. Loaded
policy and pending preview are discarded on caller/profile change without automatic reload.
The server enforces Manage access, partition, runtime write fence, row version, bounded
shortening, policy audit and transaction. Do not replace them with a disabled button.

The current form deliberately disables edits during its operation; this local behavior is
compatible with extraction and does not justify globally freezing unrelated Settings sections.
Raw invalid numbers and their validation messages must survive normal rendering. Confirmation
is tied to the previewed values/version, never a later changed draft or a stale profile.

## 8. Testing impact and limits of this review

Existing policy tests were fully read (WS12). They cover explicit loading, future-only update,
shortening consent, oversize previews, invalid values, profile/auth changes and the actual
Settings route. Default-branch search located additional consumers (WS16); Codex must inspect
them on the current checkout before choosing filters. Absence from a small search is not
proof a test does not exist.

The extraction can touch workspace API schema consumers, secret/reference/delete/vault tests,
file preferences/path binding consumers and history authorization/persistence. Derive focused
unions and confirm discovery. A root/general owner protocol change is an invalidation trigger
for broader proof, not permission to call a local stub a production test.

No current database/storage/API administrative command implementation was deeply audited in
this review. Their production composition must be preserved and smoke-tested, but their
extraction is deliberately deferred. No effort estimate or total-module completeness claim
is based on file sizes.
