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

## Planned responsibility boundary

The module currently owns both capability dialog renderers, configuration parsing, setup
invocation and workspace calls. Move the actual three-step wizard, three-tab details,
shared typed configuration fields and CSS into `CanDoItAll.AgentFramework.CapabilityAuthoring.UI`.
It owns a draft and edit context per acquired lifetime, raw parse-invalid values, presentation
validation and immutable submissions. Native adapters remain in the module and call the
existing catalog and setup owners. Process, HTTP, MCP, secret and trusted path effects remain
outside the rendering graph. The leaf may depend on Models, light capability/MCP abstractions
and neutral Components; Core, Persistence, runtime implementations and modules are forbidden.

Use a small typed operation record with delegates for load, save and explicit setup. The
native and independent scenario hosts supply separate implementations. A new general service
framework or separate contracts assembly adds no boundary here. A bounded additive catalog
save result must return the accepted identity and fingerprint from the coordinated write;
legacy ID-returning callers retain their contract. Null ID still means create, and a supplied
missing ID still rejects an update. Unknown acknowledgement prohibits blind retry.

Keep teams in the existing AgentFramework.UI family with its real AgentSelectionCard and
Material icon catalog. Move metadata, icon and membership renderers there. The native owner
gets an additive metadata-only operation under its existing catalog update coordination;
membership is preserved from the record at that write. The legacy full-team upsert remains
unchanged. Parent catalog operations retain profile and opening-team authority, and distinguish
an accepted mutation from a later refresh failure.

The existing A2 definition/assignment/Verify composition remains native: existing-agent
assignment can save the whole dirty agent draft, while new-agent assignment stages locally.
Neither a setup diagnostic nor team grouping grants runtime tool authority.

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
