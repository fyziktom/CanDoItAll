# Capability authoring and technical teams (CA1)

Status: implementation in progress. PP1, PP2, A2 and PP3 remain completed slices.
The sealed CA1 execution package is under `codex/bundles/CanDoItAll_Agent_Authoring_CA1`.

## Bounded PP3 follow-up

The entry pair is main `d232c8a645acc576ca00b171b02898d9cb1f604b` and Components
`b495d4c4a28f0a6588ba10bfaa7be6e8409eae18`. The main change since the PP3 handoff
is the CA1 package itself. FileTools remains `3a080ecd31068a77c1e1bd639f7a78e21c93db85`.
The existing signed Tooltip repair is reused in its Components owner. The rebuilt Web
assembly reports `0.3.0+b495d4c4a28f0a6588ba10bfaa7be6e8409eae18`; its evaluated
static-assets manifest resolves 40 BaseLib assets. Remote Components development still
points to `4a858412d2c2a3f6123bf23d8c4584f05b47627d`: this is a verified local pair,
not remote dependency delivery. No publication is authorized.

The unchanged API documentation coverage family reproduced 22 passes and one failure,
identifying exactly `WorkflowProviderOption.modelCatalog` and `.isSourceManaged`.
Canonical XML descriptions now explain model routing identifiers/display names and source
ownership. There is no wire or selector behavior change. The rebuilt family passes 23/23.
Focused current-source checks pass Tooltip lifecycle 21/21, History UI 84/84, History host
and provider selectors 37/37, and native Workflow provider projection 1/1. Models and Web
build successfully. The complete portability scan passes final no-write enforcement with
15,254 reviewed executable-source findings unchanged.

The PP3 Responses first-data-to-terminal timing failure remains qualified. Its same-image
resume passed; neither a cause nor a clean original run was established. Any relevant CA1
rerun must capture numeric elapsed values and terminal markers without changing the bound.
Local receipts are retained under `artifacts/agent-authoring-ca1/20261003`.

## Capability responsibility boundary

The actual three-step wizard, three-tab details, shared typed configuration fields,
setup result presentation and CSS now live in `CanDoItAll.AgentFramework.CapabilityAuthoring.UI`.
It owns a draft and edit context per acquired lifetime, raw parse-invalid values, presentation
validation and immutable submissions. Native adapters remain in the module and call the
existing catalog and setup owners. Process, HTTP, MCP, secret and trusted path effects remain
outside the rendering graph. The leaf may depend on Models, light capability/MCP abstractions
and neutral Components; Core, Persistence, runtime implementations and modules are forbidden.

The typed operation record has delegates for load, save and explicit setup. The
native and independent scenario hosts supply separate implementations. A new general service
framework or separate contracts assembly adds no boundary here. A bounded additive catalog
save result must return the accepted identity and fingerprint from the coordinated write;
legacy ID-returning callers retain their contract. Null ID still means create, and a supplied
missing ID still rejects an update. Unknown acknowledgement prohibits blind retry.

Teams now use the existing AgentFramework.UI family with its real AgentSelectionCard and
Material icon catalog. Metadata, icon and membership renderers live under `Teams`. The native owner
has an additive metadata-only operation under its existing catalog update coordination;
membership is preserved from the record at that write. The legacy full-team upsert remains
unchanged. Parent catalog operations retain profile and opening-team authority, and distinguish
an accepted mutation from a later refresh failure.

The existing A2 definition/assignment/Verify composition remains native: existing-agent
assignment can save the whole dirty agent draft, while new-agent assignment stages locally.
Neither a setup diagnostic nor team grouping grants runtime tool authority.

## Renderer and caller census

| Surface | Rendering owner | Native host / operation owner |
|---|---|---|
| Wizard Identity, Configure, Review | CapabilityAuthoring.UI form and shared fields | Module CapabilitySetupWizardDialog → NativeCapabilityAuthoringHost |
| Details Identity, Configuration, Raw | Same form and fields | Module CapabilityDetailsDialog → same native host |
| MCP stdio/HTTP/SSE/logical | McpConfigurationFields and configuration codec | Native setup flow, compiler and registered MCP adapter |
| Skill file/inline/upload/registered, resources | SkillConfigurationFields, draft and bounded upload | Native catalog and skill compilation/trust policy |
| Tool process/HTTP, input/limits/side effects | ToolConfigurationFields and setup result panel | Native setup flow, tool compiler and process/HTTP implementations |
| Global capability creation/details | Shared authoring form | AgentCapabilitiesPanel; origin cancellation retained |
| Definition creation from existing/new agent | Shared authoring wizard | AgentDetailsDialog and A2 assignment/whole-draft owner |
| Team metadata/icon/member selection | AgentFramework.UI Teams family | Thin module dialogs, AgentCatalogHost and coordinated catalog owner |

The old duplicated capability configuration support, wizard setup partial and dialog CSS
are removed. Thin native dialog wrappers preserve callers and result identities. Models,
light MCP/capability abstractions and BaseLib form the new rendering graph. Core and all
effect owners remain outside it; no new general contracts framework was introduced.

The first native checkpoint passes 31 catalog/portability unit cases, 43 native component
cases (including all six real A2 composition cases), and 18 mutation/proof integration
cases on an owned PostgreSQL 18.6 disk volume. Independent mode/upload/state/boundary
coverage passes 54 cases after the browser and case-distinct JSON extension regressions. Real browser checks found and repaired numeric text
loss across tabs and a missing parent refresh after asynchronous upload/setup. Their
regressions and final frozen-suite results are recorded separately. Native setup effects,
runtime consumers and final source/published browser closure remain pending.

The team owner characterization first failed all three concrete cases: stale metadata replaced
new membership, description/icon changed after dispatch, and a deleted target was recreated.
The editor-specific coordinated metadata operation fixes those cases without changing the
legacy full-team upsert. It rejects blank/duplicate names and missing updates; membership updates
freeze their incoming IDs and retain the owner's missing-agent refusal. The focused owner suite
now passes 10/10, including legacy compatibility and group deletion without deleting agents or
a neighboring team. Independent team rendering passes 23/23. Its first run had 11 fixture-copy
assertion mismatches and one icon-text selector mismatch; those original results remain recorded
separately from the successful follow-up.

The catalog host binds dialog results to the opening team, profile and acquisition. A confirmed
mutation followed by a failed catalog read or selection callback retains a read-only recovery
action; it cannot repeat the mutation. A profile change retires owned dialogs and recreates the
catalog context. The metadata draft does not contain membership; icon results, reads and saves
from retired acquisitions cannot change a replacement. Team grouping grants no authority.

The native team/catalog/icon/child/Overview lifecycle selection passes 93/93 after fresh
discovery. The first 47-case attempt passed 43 and failed four new tests whose empty-agent
fixtures rendered the catalog's empty state; the corrected fixtures supply a real agent.
Large-desktop browser evidence confirms 90 real cards in a bounded scrolling picker, a
keyboard-confirmed nested icon, and one metadata write preserving the separate member write.
The C2 full scan covers 8,328 files and 33,908 findings. Nine added/nine stale case-policy
findings were reviewed as name uniqueness, label filtering/sorting and moved renderers;
final no-write enforcement passes with 15,193 allowances. Documentation passes 351 files.

## Proof and checkpoints

Use isolated form/state tests for raw values, extension data, setup origins, uploads, accepted
identity and stale callbacks; preserve the existing real DialogHost/native composition tests.
Evaluate all project descendants before and after, including protected PP1/PP2/A2/History,
Workspace and Projects sandbox graphs, rejecting missing references and cycles. Components,
CodeAnalytics and dotnetwatch MCPs are unavailable in this session; explicit source/caller
discovery, evaluated MSBuild and native CLI/browser checks provide the documented fallback.

After each coherent family, build production then owning tests and verify discovery before
execution. Run source and published independent hosts at 1920×1080, plus repeated visible
Razor/C#/CSS probes with restored source. Native setup, saved definitions, two-owner concurrency,
runtime allow/deny, files/History and shared-provider Workflow/TestLab journeys use the final
source pair and unique owned fixtures. Shared catalog changes trigger one final frozen Stable
decision and checkpoint. This record will be updated with actual closure results; the plan is
not evidence that those obligations have passed.

The C3 independent browser selection passes all four source/published cases at 1920×1080,
scale 1. It traverses all 24 capability scenarios, actual skill upload and three-step creation,
invalid numeric input across tabs, two drafts, nested icons, keyboard submission, and 90-member
scrolling. Held-load routes initially stalled in server prerender; both scenario hosts now
start their interactive routes without prerender. This keeps held operations controllable.
Original timeouts and the later harness text-assertion correction remain in the receipts.

The evaluated final production closure is 169 projects; the capability rendering leaf has ten
and its sandbox eleven. The team/History sandbox stays at sixteen. Protected PP1/PP2/A2,
Workspace and Projects closures remain 10/5/18/8/6, with no unresolved references or cycles.
Nine visible development-loop probes use sandbox PID 73488 throughout. Razor application
times were 1841/422/384 ms, C# 144/30/21 ms, and CSS 600/413/299 ms. C# probes reacquired the
scenario after application; two browser observation upper bounds include orchestration delay.
All three probe sources were restored byte-for-byte and the owned watch process was stopped.
These are local small-host observations, not a general Web performance claim.

The native team browser journey passes canonical create/reopen/icon/member/deletion checks,
including a second native owner changing membership while metadata remains open. Agent models
and the neighboring group remain identical. The early test attempted editing before database
startup context initialization finished; waiting for the existing readiness signal resolves
the canceled-dialog attempt without weakening profile cancellation. Native setup/parent/runtime,
fresh multi-instance consumers and the triggered final Stable campaign remain separate gates.

The actual remote MCP approval journey exposed a pre-existing checkpoint gap after one real
invocation: the installed MCP SDK returns AI content with native raw representations, while the
MAF protocol codec supported only OpenAI and Ollama raw models. The bounded codec addition uses
the MCP serializer for its content/resource types and records their installed package version.
Unknown raw objects, foreign shapes and incompatible versions still require explicit recovery;
existing OpenAI/Ollama checkpoint fingerprints remain readable. No admission or approval policy
was broadened. The owned runtime fixture uses the existing `mcp_` classification family; its
original unclassified name was truthfully denied before dispatch. The original native failure,
including its single external effect and reconciliation requirement, remains retained separately.
