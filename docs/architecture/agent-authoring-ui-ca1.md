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

The next checkpoint keeps teams in the existing AgentFramework.UI family with its real AgentSelectionCard and
Material icon catalog. Move metadata, icon and membership renderers there. The native owner
gets an additive metadata-only operation under its existing catalog update coordination;
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
| Team metadata/icon/member selection | Module at this checkpoint | AgentCatalogHost and coordinated catalog owner; extraction follows |

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
runtime consumers, teams and final source/published browser closure remain pending.

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
