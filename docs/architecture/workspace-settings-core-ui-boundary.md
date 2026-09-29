# Workspace Settings Core UI boundary

Completed `CanDoItAll_Workspace_Settings_Core_UI_Decoupling` on the current checkout.
Entry: `components-decoupling` at `ba6192b7982383b49a042291f8c47833b79c98e4`, clean tree.
The only change since the reviewed Resources implementation is the sealed input package.
The package is historical input; this maintained document owns execution decisions and proof.

## Scope and architecture decision

This slice closes Resources RS-R1 and extracts the Settings shell and four complete sections:
Workspace defaults, Secrets, Files and Provider history policy. Data Sources, Storage and
API Access remain active production hosts. Providers remains a replacement navigation to
`/agents?tab=providers`. This is partial Workspace completion.

The selected proof tier is Behavioral, including the package's required real-owner,
production browser, standalone publish and development-loop observations. No other module
extraction, HTTP rewrite, schema migration, provider feature or vault redesign is authorized.

| Responsibility | Existing owner | Placement |
| --- | --- | --- |
| Route tokens, navigation, profile/authentication subscriptions, deferred sections | SettingsPage | Thin production route with active-section composition |
| Six defaults values and provider choices | Mixed Workspace DTO/EF/service file | Workspace.Contracts, retaining namespace, defaults and API descriptions |
| Secret metadata and transient explicit editor values | SecurityModels | Existing light Security.Abstractions; vault, resolver, references and transactions stay in Security |
| Defaults/secret/file drafts, admission, captured operations and read generations | SettingsPage and Files panel | Separate cohesive presentation controllers shared with the sandbox |
| File preference normalization, host binding, migration and durable writes | Infrastructure control plane | Existing owner behind a narrow Workspace projection/port |
| Versioned history policy and bounded retention | ProviderHistory.Abstractions and persistence | Reuse existing policy protocol; shared presentation plus production authority/lifetime host |
| Four forms, shell, masking/copy and confirmation | Workspace Razor module | Workspace.UI with BaseLib and light contracts |
| Controlled waits, faults and synthetic state | None | Workspace.UiSandbox; no production DI or backend implementation references |

The project boundary is necessary because the original module directly references
Infrastructure, Projects and Security. A file move within that module would retain the
heavy graph. Narrow owner adapters and four independent controllers are justified by
different authority, sensitivity and lifetime rules. A universal settings snapshot, service
bag, new partial-class cluster and duplicated sandbox state machine are rejected.

Defaults and Secrets retire with the canonical profile; history also retires with caller
authority. Files remains machine-local. Presentation receipts contain safe identities and
stages only, never plaintext, full secret commands, vault keys or arbitrary metadata JSON.
Known durable stages are recorded at the real owner; observation does not prove request
causality. Contracts must not reference implementation projects or persistence.

## Composition and proof

Preserve the existing compact header and eight navigation items. Each selected section is
the primary working surface. Supporting counts remain CompactStat values, Secrets retains
its list/detail workflow, Files retains its short editor and override list, and history
uses a compact separate confirmation dialog. BaseLib owns layout, fields, masking, copy,
dialogs and transient disclosure. The page/body is the intended scroll owner at 1600x1000;
inspect open confirmation and selector states. Preserve masked drafts through section
switches while unmounting every revealed secret renderer.

CodeAnalytics, Components MCP and dotnetwatch MCP are unavailable in this session's tool
inventory. Source/contracts, evaluated restore graphs, direct builds, actual Playwright and
owned `dotnet watch` processes provide the corresponding evidence. Sibling sources are read-only.

Initial toolchain: SDK 10.0.303, Windows, source dependency mode. Components
`f258ab6a959a97fa16c01d0858e7dc122728a11a`, FileTools
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`, SharedInfo
`83e21e23bcf43d92b061a6d367ac385241d13cd3`; all siblings clean at entry.
SharedInfo tooling, .NET, documentation and layout standards apply with the repository's
existing source-reference conventions. Build configuration: `WorkspaceSettingsUiProof`.

Independent tests must exercise each shared controller and the actual renderer without the
Workspace implementation. Negative graph fixtures must reject forbidden transitive and
unresolved references. Production proof covers real PostgreSQL/vault/control-plane owners,
existing strict HTTP settings semantics, history authority/versioning and all deferred hosts.
Test selection follows current consumers and build-backed discovery, never inherited counts.
Changes to Security outcomes widen proof to actual Security consumers; file changes widen
proof to host-path/migration tests. Evaluate broad Stable invalidation after the final owner
contract changes are known. Portability, documentation/evidence and secret gates are required.

## Implementation and owner semantics

RS-R1 is repaired. Catalog/reference readiness is separate from exact editor readiness.
Held or failed exact loads cannot admit placeholder Save/Delete, a failed same-ID selection
can retry, acquired dirty editors survive optional-reference failures, and production Agent
context follows the exact route editor. The accepted Memory origin regression remains green.

Workspace.Contracts keeps all six defaults and the provider-choice protocol in their old
namespace. Security.Abstractions now owns the unchanged secret enum, metadata DTO and transient
editor DTO. Four shared controllers replace the production page's draft/read/write logic;
production adapters retain the existing Workspace, Security, local file and history owners.
No schema, project/file admission, provider feature, cryptographic protection or resolver was
replaced. The existing two Razor code-behind pairs remain thin framework components, not a
new partial service cluster.

| Surface | Completion boundary | Production lifetime / authority |
| --- | --- | --- |
| Shell | Eight navigation items, direct query tokens, invalid/missing default, replacement Providers redirect | Production route; active-only deferred slots |
| Defaults | Six immediate inputs, independent reference reads, stable EditContext, per-field reconciliation | Canonical profile-bound Workspace owner; old effects never populate a successor |
| Secrets | Metadata list/filter, explicit exact editor, actual reveal/copy, captured commands, exact-ID metadata observation | Security owns vault, metadata, references, locks and runtime resolution; profile/caller retirement removes sensitive editor references |
| Files | Captured normalized extension, active/rebind state, existing-path validation, save/delete and read-only observation | Existing machine-local control-plane owner, including its documented lazy migration on List; never launch an executable |
| Provider history | Explicit Load, raw invalid input, versioned future policy, bounded preview and separate confirmation | Existing Manage authorization, partition/version/write fences, transaction and audit remain authoritative |
| Data Sources / Storage / API Access | Working production hosts, not extracted | Original services and panels; no sandbox claim for their persistence or authority |

Operation receipts are bounded to 16 and cannot evict unresolved writes. They hold identities,
action, origin, policy version and safe outcome/diagnostic only. Metadata observations retain
the original known/unknown outcome and stage even if observation fails. Unknown creates expose
an explicit candidate-ID observation control; observation never resolves plaintext or claims
request causality. Same-origin/conflicting writes are blocked through reconciliation. Concurrent
refresh callers await the same read; independent defaults/provider reads do not serialize.

Bounded owner corrections:

- Workspace copies all six values before awaiting, publishes currency only after successful
  persistence, and exposes a known normalized saved snapshot if Activity/logging fails.
  HTTP still strictly rejects unknown/missing/invalid fields and retains scopes, provider
  admission, six-field replacement and `X-CanDoItAll-Read-Back: pending` behavior.
- Security copies and clears its private command, adds an editor-only require-existing entry
  point, and reports redacted exact commit stages after metadata persistence. Non-UI Save
  retains its existing upsert behavior. A lost SaveChanges acknowledgement retains the staged
  vault payload because metadata might already reference it; it is unknown, never a false
  refusal. This can leave an orphan payload on an actual failed commit; no unsafe speculative
  cleanup or automatic retry was introduced. Known postcommit cleanup/Activity failures remain
  warnings. Existing deletion reference checks, transaction and serialization locks remain.
- File preferences report a known durable result if subsequent logging fails. Their durable
  writer, host-binding/rebind rules, migration, resolver and actual launch path are unchanged.

## Behavioral proof ledger

Commands use `WorkspaceSettingsUiProof`, SDK 10.0.303, sibling source mode and `/m:1`.
The post-checkpoint shell correction uses the independent shorter configuration `WsCoreProof`
to avoid changing binaries in the running Stable gate.
For each focused row, discovery is build-backed `dotnet test <project> --list-tests --filter
<filter>` followed by the same filter with `--no-build --no-restore`, with TRX output. Expected
counts came from the current Fact/InlineData declarations; these selections have no unresolved
MemberData expansion. Artifacts are ignored diagnostic evidence, not shipped credentials.

| Selection / project | Expected / discovered / execution | Receipt |
| --- | --- | --- |
| Resources light / Resources.UI.Tests, `FullyQualifiedName~ResourcesUi` | 70 / 70 / 70 passed, 0 skipped | `s0-resources` |
| Resources exact editor + Agent context + owner postcommit / main Components | 21 / 21 / 21 passed, 0 skipped | `s0-resource-host` |
| `MemoryResultOriginTests` / Memory.UI.Tests | 8 / 8 / 8 passed, 0 skipped | `s0-memory-origin` |
| Workspace light / Workspace.UI.Tests, `FullyQualifiedName~WorkspaceUi` | 38 / 38 / 38 passed, 0 skipped; repeated after formatting in `WsCoreProof` | `workspace-light-rendering`, `workspace-light-closure` |
| `ProviderHistoryPolicyPanelTests`, `SettingsPageDataSourcesTests`, `SecretProviderSelectionTests`, `ApiTokenAdministrationTests`, `ApiUserAdministrationPanelTests`, `StorageCatalogSelectionComponentsTests` / main Components | 32 / 32 / 32 passed, 0 skipped | `workspace-production-components-fixed` |
| Security/FileTools/history unit consumers below / main Unit | 108 / 108 / 108 passed, 0 skipped | `workspace-unit-consumers` |
| Integration consumers below / main Integration | 169 / 169 / 168 passed, 1 test-timing error, 0 skipped; corrected owner selection 10 / 10 / 10 passed; subsequent full Stable Integration 3,242 passed | `workspace-integration-consumers`, `workspace-owner-retirement`, `stable-summary.json` |
| `WorkspaceSettingsBrowserTests` / Playwright | 2 / 2 / 2 passed, 0 skipped; final production-only case repeated 1 / 1 / 1 passed after shell correction | `workspace-browsers-final`, `workspace-production-closure` |
| `CanDoItAll.Tests.Components.Shell.SettingsRendererTests` plus `WorkspaceApiStatusTests` / main Components | 7 / 7 / 7 passed, 0 skipped, in `WsCoreProof` | `workspace-shell-review-fixes` |
| `MafDependencyDirectionArchitectureTests`, `ProviderBoundaryArchitectureGuardTests`, `HostPlatformTestClassificationTests` / main Unit | 24 / 24 / 24 passed, 0 skipped, including five namespace/dependency scanner fixtures | `workspace-architecture-closure-serial` |
| `WorkspaceCoreOwnerOutcomeTests&Category=HostPlatform` / main Integration | 10 / 10 / 10 passed, 0 skipped | `workspace-owner-platform-closure` |
| Standalone published Production sandbox | Four actual sections, assets, copy/reveal/remask, policy confirmation, original-store completion after reset passed | `published-proof.json` |

Unit filter: OR the class names `SecretVaultTests`, `SecretRuntimeAuthorizationTests`,
`SecretReferenceSurfaceTests`, `SecretPortabilityTests`, `SecretMigrationTests`,
`WorkflowHttpSecretUseTests`, `FileApplicationPreferenceServiceTests`,
`ProviderFeatureMatrixTests`, `ProviderHistoryPolicyTests` with `FullyQualifiedName~`.

Integration filter: OR `WorkspaceCoreOwnerOutcomeTests`, `WorkspaceSettingsHttpOutcomeTests`,
`SecurityOwnerPersistenceTests`, `SecretPortabilityIntegrationTests`,
`ProviderSecretTransferOwnerTests`, `PluginSecretBrokerIntegrationTests`,
`WorkflowHttpSecretAdmissionIntegrationTests`, `WorkspaceSettingsOwnerPersistenceTests`,
`ProviderHistoryPersistenceIntegrationTests`, `ProviderHistoryAuthorizationIntegrationTests`,
`ApiSectionPermissionsTests`, `ApiAccessContractTests`, `ApiSessionBoundaryTests` with
`FullyQualifiedName~`. The corrected retirement test waits after real metadata commit,
not before the cancelable vault write. A second defaults retirement case was added; all ten
owner outcome cases now pass. The 160 other integration cases passed with unchanged owner
semantics; their source-derived inventory and receipts remain separate from the corrected run.

Failing-first evidence: ten RS-R1 cases produced seven failures and three passes before
repair (`s0-failing-first-bounded`); two real owner boundary defects failed before repair;
four refresh/read independence cases failed before single-flight repair. Earlier aborted or
failed browser attempts are retained as failures: tab/copy accessible-name mismatch, missing
interactivity wait after reload, duplicate Storage text selector, and validation synchronization.
Screenshot inspection additionally repaired literal Files conditional text and constrained
Secrets header action layout; actual empty/populated renderer assertions cover the former.
The final shell review reproduced two stale API badge cases: after a successful Open/JWT
status read, a later failed activation retained the prior badge. The deferred host now emits
an explicit unavailable status and a redacted warning. Both failures reproduce in
`workspace-api-status-reproduction` and pass with recovery/lazy-read assertions in the
seven-case shell selection. The initial reproduction hit a bUnit find/click race; it is a
setup failure, not the regression receipt. Event lookup/click now share the renderer context.
The first separate configuration name exceeded Windows' 260-character template-copy path
limit before discovery; `WsCoreProof` fixes the proof setup without changing repository build
settings. All original failed attempts remain diagnostic artifacts.

The main host browser uses an ephemeral port, private PostgreSQL/vault/control-plane roots,
real default and secret saves/deletes, harmless existing executable fixtures (never launched),
history update/confirmation/reload, all three deferred hosts, direct URLs, back/forward and
Providers navigation. The source sandbox proves caret/focus through a held provider refresh,
zero automatic policy reads, 30-second SecretField masking and raw invalid numeric input.
Published proof uses its DLL in Production with no database/vault configuration and separately
checks theme, font, scoped styles and dialog/copy assets.

## Validation matrix traceability

| Package obligations | Concrete proof |
| --- | --- |
| V-RS-01–05 | ResourceEditorReadinessTests, ResourcesExactEditorHostTests, Agent context and existing owner host selection |
| V-RS-06 | Eight MemoryResultOriginTests; no Memory source change |
| V-BD-01–04 | Original owned host screenshots/watch/graph; shared renderer production route; runtime/public-type negative graph tests; evaluated graph below |
| V-BD-05–08 | Existing API grants/contract/session tests, new real HTTP committed-warning case, real navigation and deferred host browser, active-slot component test, product/test/CI membership and publish |
| V-DF-01–06 | WorkspaceDraftTests, WorkspaceRefreshTests, WorkspaceReceiptTests, WorkspaceCoreOwnerOutcomeTests, WorkspaceSettingsOwnerPersistenceTests, strict HTTP tests |
| V-SE-01–09 | Real SecretField bUnit/browser, captured and retired editor tests, metadata-only observation, real Security stage/acknowledgement tests, Security and Workflow/Plugin/transfer consumers, redaction assertions and artifact review |
| V-FI-01–04 | Held actual forms, FileApplicationPreferenceServiceTests private-root paths/migration/rebind/logging failures, preserved stale list and machine-local owner |
| V-HI-01–06 | History controller/form and existing production panel tests; policy persistence/concurrency/retention/authorization tests; actual bounded dialog and versioned production update |
| V-UI-01–05 | Source and published scenario host, real production browser, inspected desktop/overlay evidence, measured watch loop below |
| V-CL-01–04 | Direct builds, build-backed selection ledger, static/docs/secrets gates, named Stable checkpoint and exact cleanup below |

## Development-loop and graph observations

| Host | Evaluated projects including root | Packages | Native assets | Unresolved / cycles |
| --- | --- | --- | --- | --- |
| Original Web | 150 | 140 | 30 | 0 / 0 |
| Extracted Web | 153 | 140 | 30 | 0 / 0 |
| Workspace sandbox | 8 | 1 framework assets package | 0 | 0 / 0 |

Actual restore dgspec edges and project.assets.json determine these counts, including local
source replacement. Runtime/public-contract tests independently reject a forbidden transitive
implementation and an unresolved edge. The full Web remains broad; the sandbox is the small
loop. Measurements use three visible edits per applicable class, byte restoration in finally
and owned process trees. No selected scoped stylesheet or new feature JS exists, so those edit
classes are N/A; shared JS behavior still has source and published browser proof.

| Host | Startup observation (ms) | Razor edit-to-visible samples (ms) | C# edit-to-visible samples (ms) |
| --- | --- | --- | --- |
| Original Web | 74,878.3899 to interactive | 9,978.1625; 4,962.5189; 3,936.2979 | 4,103.8883; 2,378.6192; 2,377.3895 |
| Extracted Web | 88,749.2511 to confirmed first Save | 3,950.0217; 2,919.7736; 1,903.2606 | 2,120.6385; 2,771.8483; 2,365.9625 |
| Sandbox | 11,026.5393 to confirmed first Save | 4,919.7566; 377.3129; 361.8739 | 345.1099; 341.3238; 343.7788 |

These are single startup observations and three successful edits of each applicable class,
not a statistical benchmark. Original startup ends at interactivity; the later probes also
perform a confirmed Save, so those startup milestones must not be compared as equivalent.
Original/extracted Razor medians are 4,962.5189/2,919.7736 ms and C# medians are
2,378.6192/2,365.9625 ms; sandbox medians are 377.3129/343.7788 ms. No full-Web percentage
speedup is claimed. The Web fixture uses private PostgreSQL; the sandbox uses in-memory
scenario state. Both render the actual forms at 1600x1000 with Parity assets. The measured
Razor heading and executed C# projection use restored source bytes; neither required a
restart. Failed baseline/probe attempts remain separate, including a discarded sandbox
observer setup that mistakenly requested PostgreSQL before the backend-free observer ran.
The final Secrets header-only layout correction does not change either measured edit path.

Receipts: `original-web-complete-loop.json`, `extracted-web-loop.json`, `sandbox-loop.json`
and their watch/observer logs. Actual `dotnet watch --list --project <host.csproj>
--configuration WorkspaceSettingsUiProof` inventories are summarized in `watch-inventories.json`:

| Host | Total watched paths | C# | Razor | CSS | BaseLib paths |
| --- | --- | --- | --- | --- | --- |
| Original Web | 4,490 | 3,414 | 631 | 123 | 231 |
| Extracted Web | 4,520 | 3,431 | 641 | 123 | 231 |
| Sandbox | 285 | 73 | 181 | 8 | 231 |

The sandbox watches all feature contracts/controllers/renderers plus BaseLib source and
BaseLib generated styles. Its linked application `output.css` is an explicit source/publish
content asset but is absent from `dotnet watch --list`; authoritative `Tailwind/input.css`
and its imported theme files are also outside that .NET inventory. After editing those
inputs, run `npm ci --prefix Tailwind` when dependencies are absent, then
`npm run tailwind:build` (or a separately owned `npm run tailwind:watch`), rebuild/restart the
sandbox and refresh the browser. This is an explicit theme workflow limitation, not a
measured CSS hot-reload result or permission to add a Web project reference.

## Desktop inspection

The original and final production defaults, masked Secrets, Files, history and confirmation
screens were inspected, together with source/published sandbox screens and all three deferred
production sections. History's two-column form, notices and primary actions fit the first
1600x1000 viewport; its approximately 512px confirmation dialog has readable content and visible
Cancel/Apply actions. Defaults and Files remain primary working surfaces rather than summary
dashboards. Secrets keeps the 24rem list rail, full editor, readable New action and masked value;
Save/Delete sit below the long editor and remain reachable through the existing page scroll.
Scenario controls add height in the sandbox, so those actions are not claimed to fit its first
viewport. There is no nested scroll container introduced by this slice. Real browser actions
exercise controls beyond the viewport. The unchanged deferred Data Sources header still has a
narrow PostgreSQL action label; its editor/actions function in bounded production smoke proof,
and redesign of that deferred surface is outside this extraction.

No retained screenshot shows revealed plaintext or a copied payload. Real copy/reveal assertions
compare synthetic values without retaining them in screenshots. Final production and published
screens confirm the Files rendering and Secrets header corrections. Source proof additionally
checks caret/focus under held reference refresh and the actual 30-second mask timeout.

## Final gates

The named checkpoint `workspace-settings-core-final` is triggered by the shared Security
DTO/outcome protocol and product/test solution membership. Both solution builds passed.
The original `.slnx` files reject an arbitrary configuration name before building (MSB4126).
Task-owned copies resolve paths to the original sources and add only `WorkspaceSettingsUiProof`;
`solution-proof-membership.json` verifies identical 149-product/14-test-project membership and
records both source hashes. Repository solution configurations remain unchanged.

Build-backed discovery found 15,516 named cases across all 14 expected test assemblies, including
38 Workspace light, 10 owner-outcome and one HTTP-outcome case. The one broad execution uses
`Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true`
with `--no-build --no-restore`, `/m:1`, private PostgreSQL 18 and `WAL_LOG`.
All 14 assemblies completed: 15,571 executed, 15,567 passed, four failed and zero skipped.
The main component failure was the existing Workspace fallback-renderer test sending
`onchange` to an immediate `oninput` field. Its event was corrected without changing the
renderer, and all five class cases pass in the seven-case shell review selection.
The three Unit failures exposed a source guard treating the preserved Security namespace
declaration as a dependency, a guard reading the old provider-catalog path, and a missing
HostPlatform category on the new vault-owner test class. All three are repaired and pass
in the 24-case guard selection. The classifier itself is unchanged; the ten owner cases
are now discovered and pass with the actual HostPlatform filter.

| Stable assembly | Discovered | Executed | Passed / failed / skipped |
| --- | --- | --- | --- |
| Collaboration.UI.Tests | 25 | 25 | 25 / 0 / 0 |
| Memory.UI.Tests | 36 | 36 | 36 / 0 / 0 |
| Plugins.UI.Tests | 50 | 50 | 50 / 0 / 0 |
| Resources.UI.Tests | 70 | 70 | 70 / 0 / 0 |
| SchedulerPlanner.UI.Tests | 59 | 59 | 59 / 0 / 0 |
| TestLab.UI.Tests | 38 | 38 | 38 / 0 / 0 |
| Tests.Components | 2,378 | 2,378 | 2,377 / 1 / 0 |
| Workspace.UI.Tests | 38 | 38 | 38 / 0 / 0 |
| Tests.Integration | 3,237 | 3,242 | 3,242 / 0 / 0 |
| AgentFramework.Memory.Tests | 22 | 22 | 22 / 0 / 0 |
| Memory.Tests | 187 | 203 | 203 / 0 / 0 |
| Collaboration.Tests | 35 | 35 | 35 / 0 / 0 |
| TestLab.Tests | 29 | 29 | 29 / 0 / 0 |
| Tests.Unit | 9,312 | 9,346 | 9,343 / 3 / 0 |

The 55 additional executions are seven MemberData expansions, each discovered as one case:
plugin simulation 6; Memory cross-caller denial 5 each for two theories; Memory ownership
dimensions 9; floating-chat invalid settings 8; finalizer schema contracts 8; process tool
host routes 21. Their current source providers and actual TRX rows agree
(`memberdata-expansions.json`, `stable-summary.json`). No empty filter or skip is counted as
passing proof.

The measured broad execution took 9,276.9256 seconds (154.62 minutes); the solution-build,
discovery and execution closure took 9,411.5997 seconds (156.86 minutes). Integration's TRX
elapsed time is 7,377.4611 seconds (122.96 minutes). The run shared the local host with the
bounded post-checkpoint proofs. Current CI separates Unit/Memory jobs (90-minute Linux and
120-minute Windows/macOS budgets) from Components/Integration shards (90-minute Linux and
180-minute Windows/macOS budgets). This serial Windows measurement is not a measurement of
those sharded jobs or proof of their cross-platform timing. No CI budget or exclusion changed.

The frozen source manifest and `post-stable-review-delta.json` identify the later bounded
changes: the shell's unavailable-status callback/logging, its two new regression cases,
the stale test event, whitespace-only formatting of three renderers, removal of an extra
final blank line in the Resources regression test, the two source guards and the owner-test
platform category. Owner protocols,
project membership, dependency direction and shared test infrastructure remain unchanged.
Those later changes use fresh targeted builds/tests, actual production browsing and a fresh
standalone publish. They do not justify another full Stable execution at this checkpoint.
A mixed failed broad receipt and green focused repair are not described as one clean broad run.
The Security guard still rejects imports, aliases and qualified product-module references;
five scanner fixtures prove that distinction. Zero-project-reference and actual contract
assembly/reference assertions retain the dependency boundary. The provider guard reads the
moved light catalog, checks its ownership exclusions and requires the old implementation
provider directory to contain no source files. These are test-maintenance changes, with no
shared fixture or production owner change requiring another full Stable execution.
The first guard proof hit CS2012 because two builds shared the same output configuration;
its failed discovery log is retained. A sequential build/discovery/run passed all 24 cases.
The final staged review also removed trailing whitespace from two renderers; the three cleanup
files retain identical non-whitespace bytes (`final-whitespace-review.json`). The UI direct
build passes again with zero warnings or errors (`final-whitespace-ui-build.log`), and the
staged diff check is clean. These whitespace-only edits do not reopen behavioral tests.

Twelve affected production roots passed direct builds: Security.Abstractions, Infrastructure,
Security, Workspace.Contracts, Workspace.UI, Workspace.Presentation, Workspace, Resources.UI,
Resources.Presentation, Resources, Web and Workspace.UiSandbox. The four roots touched by the
final shell/formatting correction were rebuilt directly in `WsCoreProof`, then 38 light,
seven shell and one real-production browser case passed. `publish-closure.log` and
`publish-closure-probe.log` prove a fresh `published-closure` directory running in Production
without database/vault configuration; its four sections, assets and interactions pass.

Full proposed-tree portability was regenerated with `--include-untracked` after the final
source and documentation edits; the complete file/finding inventory is retained in the
final scan receipt. Review of the original delta accepted 26 added and
16 stale keys after checking every finding: typed path projections/captured file commands,
existing Security owner seams and local metadata filtering are intentional; moved/deleted
monolith lines and the Windows-specific placeholder explain stale entries. Markdown command
fences are documented scanner false positives. No scanner pattern/policy changed. The baseline
diff contains only those reviewed allowance deltas and generated counts (15,186 to 15,198).
The final no-write enforcement passes with all 15,198 reviewed executable-source findings
unchanged (`portability-final-tree-scan.*`, `portability-final-tree-review.log`). The
additional raw finding in the final test-only repair is the deliberate forbidden-reference
scanner fixture naming SecretService; it is test input and introduces no host dependency.
Baseline fixtures
pass six cases and artifact-secret-scanner fixtures pass four.

The maintained documentation gate passes for 285 Markdown files; its evidence fixtures pass
all nine cases. The final artifact-secret scan passes with zero findings and zero oversized
or unreadable text files (`secrets-final.json`, `secrets-final.log`). Its 200,000,000-byte cap
includes the 117,246,667-byte Integration TRX; an earlier 100 MB run that skipped that file
is retained as failed coverage, not passing evidence. The report inventories supported text
evidence and excluded file types; screenshots were inspected separately as described above.

Six credential-shaped static test values were reviewed in security-scanner, audit-redaction
and invalid-endpoint fixtures. Fifteen discovery display names and 30 corresponding TRX
name attributes were redacted across two files; method identity, test IDs, counters and
outcomes remain unchanged. `artifact-redactions.json` records each transformation's hashes
and reviewed sources. Scanner rules and test outcomes were not altered. The initial
findings and the failed coverage report are retained for provenance.

Owned cleanup is complete. The disposable PostgreSQL 18.6 container's full ID, owner label,
name, image, loopback endpoint and auto-remove setting were verified before shutdown; its
absence was then verified (`database-cleanup.json`). All seven recorded watch/published
host process IDs and six private loop roots are absent (`process-cleanup-final.json`).
Browser fixtures disposed their private hosts/vault/control-plane roots, and no executable
fixture was launched. Build outputs and masked evidence are retained. The ordinary
application/database were not restarted or stopped. Components, FileTools and SharedInfo
remain clean at the entry revisions recorded above.

## Raw evidence and scope limits

Ignored raw evidence: `artifacts/workspace-settings-core-ui`; masked browser screenshots:
`output/playwright/workspace-settings-core-ui`. Package integrity passes for 38 files,
37 manifest entries and 33 sources; root utility tests pass 11 and shared utility tests 14.
The complete extracted package, including its shared utilities, was read. Package checks are
package proof only. This is a compatible externally shaped Behavioral bundle: readiness and
closure map its input/review, S0/W1–W5 requirements, matrix and source register to this maintained
record; imposing the canonical bundle folder schema would mutate sealed historical input.

Native Unix vault/keyring, installed-app startup, real provider accounts and mobile layouts
are outside this Windows desktop slice. Existing provider and storage refusals remain intact.
Neither a synthetic sandbox store nor a screenshot establishes owner persistence or authority.

## Bundle semantic closure

Shape: compatible external Behavioral bundle. `prompt.md` and source review preserve the
inputs/current state; S0 and W1-W5 supply ordered work units; the shared foundation defines
boundaries; the validation matrix supplies acceptance/proof; this maintained record owns
execution status and closure. The canonical structural validator is not applicable to the
sealed external shape. The complete package's utilities/integrity checks are separate input
proof. No package files were modified.

| Work unit / raw requirement | State | Evidence / remaining action |
| --- | --- | --- |
| S0: reproduce and repair RS-R1 before Workspace wiring | Solved | Seven failing-first cases; 70 light, 21 routed/owner and eight Memory-origin cases pass |
| W1: current-source inventory and original real-host baseline | Solved | Entry/review drift, source consumers, original screenshots, evaluated/watch graph and edit samples |
| W2: light contracts, actual renderer boundary and composition | Solved | Four projects, direct builds, negative closure fixtures and production DI |
| W3: defaults, Secrets, Files and history lifetime/owner behavior | Solved | Independent controls/controllers plus actual owner, HTTP, vault/control-plane and policy consumers |
| W4: real active production/deferred slots and mutable sandbox | Solved | Production route/browser, lazy-slot/status tests, source sandbox and fresh Production publish |
| W5: current evidence, gates and exact cleanup | Solved | All 14 Stable assemblies finished; all four reported failures repaired and revalidated; focused/build/browser/portability/docs/secrets proof and exact cleanup complete |
| Scope: preserve deferred Data Sources/Storage/API Access and capability refusals | Solved | Production panels and existing owner checks retained; no additional module extraction |

Final bundle-validator decision: Pass for this compatible external Behavioral bundle.
Changes to an owner protocol, public contract, composition boundary or shared fixture reopen
its source-derived consumer selection and broad-trigger assessment. A renderer-only correction
reopens its light tests and relevant browser/asset proof. A documentation-only update reopens
documentation validation; it does not justify replaying the entire Stable gate.

## C# Architecture Gate Result

Status: Pass

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Resolved | Catalog readiness previously admitted a placeholder Resources editor | Seven failing-first RS-R1 cases; 70 light and 21 owner/route cases pass | None |
| Resolved | Delayed writes and secondary failures required explicit original-owner facts | Captured-command, real-owner stage, lost-acknowledgement and retirement tests | None |
| Resolved | A failed API status activation retained a prior Open/JWT badge | Two failing-first cases; both pass including recovery and no eager read | None |
| Resolved | Existing source guards and new owner-test platform classification needed extraction-aware validation | 24 guard cases, including dependency-check fixtures, and ten HostPlatform owner cases pass | None |

### Dependency direction

Contracts reference the existing Security abstraction only. UI references light contracts,
History.Abstractions and BaseLib. Presentation references UI/contracts, while production
adapters compose the existing owners. The standalone sandbox resolves eight projects,
one framework-assets package, zero native assets and no forbidden/unresolved/cyclic edge.
It shares the shipped controllers/renderers; no broad facade, service locator, duplicate
workflow, production fake registration or backend transitive escape was introduced.

### Partial-class policy

The existing route and history Razor/code-behind pairs remain thin framework components.
No new partial service cluster or manager was added. Existing Security and Workspace owners
retain their responsibilities and their existing partial organization.

### Testability proof

Thirty-eight independent light cases cover real renderers/controllers and negative graph
fixtures without production implementations. Production DI, real PostgreSQL/vault/control-plane,
HTTP and history authorization/retention consumers supply the owner proof. Source and published
browser checks use the same renderers, real child controls and active deferred production slots.
No synthetic store, source assertion or screenshot is substituted for owner persistence.

### Closure decision

The architecture boundary passes. S0 and the four selected sections are implemented and their
focused owner, renderer and browser proofs pass. The named Stable execution is complete;
its four test-maintenance failures are repaired with current build-backed focused proof.
Documentation/evidence, portability and artifact-secret checks pass, and owned cleanup is complete.
Data Sources, Storage and API Access remain deferred production hosts; no further module may
be started as part of this bundle.
