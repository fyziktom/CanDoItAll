# Scope and dependency architecture

## A meaningful larger slice

This run completes TWO related administration families, each with its own host state and proof:
capability-definition authoring and technical-agent teams. It does not combine their durable
models, create a generic administration engine, or reopen completed technical-agent A2.

| Family | Actual rendering included | Native work retained |
|---|---|---|
| Capability creation | All three wizard steps, MCP/Skill/Tool choices, all existing config fields, bounded SKILL.md upload, preview/review and setup diagnostics | Catalog writes, trusted compilation, process/HTTP/MCP setup execution, path/secret resolution |
| Capability details | Identity/Configuration/Raw, built-in restrictions, typed MCP/Skill/Tool editors, tags, setup input/results, explicit error/retry/save | Exact capability lookup, ExpectedFingerprint check, proof status and catalog commit |
| Teams | Metadata editor, actual icon picker, actual member selection, count/search/private-provider badges and confirmations | Team metadata/membership/deletion through catalog owner; catalog reload/navigation |
| Integration | Existing capabilities catalog and nested A2 launch/result handling; existing team catalog host | Whole-agent saves, assignment, Verify and catalog operation owners |

No capability list, Agent Editor A2, provider PP1/PP2/PP3, Projects, Workspace, shared FileBrowser,
Workflow canvas or Process runtime is to be rewritten. Capability curator is a retained native
consumer, not a new autonomous agent feature. Test-chat/model-maintenance API existence does not
prove an unextracted dialog exists; classify actual live callers in the final census.

## Preferred graph

Production module -> host/adapter -> capability-authoring rendering leaf -> required light models,
capability/MCP abstractions and real shared components. Independent authoring sandbox -> SAME leaf.
Team rendering can join the existing AgentFramework.UI family and scenario host; introduce a new
team leaf only for a demonstrated useful graph boundary. Avoid a new project for each kind/tab.

Candidate authoring paths:
- src/UI/CanDoItAll.AgentFramework.CapabilityAuthoring.UI
- src/Sandboxes/CanDoItAll.AgentFramework.CapabilityAuthoring.UiSandbox

These are recommendations, not an interface/project quota. Existing namespaces and public payloads
should remain where practical. If an assembly move affects reflection/dynamic registrations or
serialized identities, migrate consumers deliberately and prove it. Reuse stable Models rather
than copying every DTO, but do not carry runtime Core through a convenient helper import.

## Configuration seam

ConfigurationEditorSupport currently contains typed states plus serializer models and references
Core policy (for example environment-name comparison) [S13]. Separate presentation drafts and
validation feedback from the owner's platform/trust-sensitive normalization. The renderer may
own ordinary input parsing; only the owner determines whether an endpoint, command, binding,
capability kind or authority is valid. Compare canonical round-trips with existing behavior.

Do not add IAgentFrameworkWorkspaceService, IServiceProvider, DB contexts, process launchers,
MCP factories, HTTP clients or secret stores to the new leaf. Do not expose an arbitrary Execute
method instead of typed intents. A narrow view contract is appropriate for this many-field
workspace; immutable presentation+intent is equally valid for small subviews.

## Composition and assets

Module wrappers may remain for DialogService, route/profile/auth lifetime and native effects.
Actual large markup and reusable config sections cannot remain hidden in those wrappers. Scope
all JS/CSS with the moved family and verify Tailwind discovery, isolated CSS, fonts and dialog
interop after independent publish. Use Components MCP where available; never copy BaseLib.

Both wizard and detail should reuse coherent typed config renderers where semantics match.
Do not force wizard-only and built-in detail behavior through accidental shared defaults.
The Material icon picker may be feature-neutral or team-specific according to existing consumers;
move it once, keep its true icon catalog, and do not generalize beyond this need.

## Protected graphs

Snapshot evaluated transitive references for PP1, PP2, A2, History, Workspace and Projects before
work. Existing consumers can reference a new genuine rendering leaf only through intended
composition, not accidental back-references. Foundation/MAF runtime never depends on UI. Tests
must fail on missing refs/cycles, not skip unresolved paths. A smaller count is evidence of
isolation, not performance proof.
