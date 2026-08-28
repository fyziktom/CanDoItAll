# SB07: Mirror thinking capabilities and curate model suggestions

## Status

- Status: Completed
- Proof tier: Governed

## Objective

Close N011/R11 and N012/R12 in code; SB08 owns live acceptance.

## Covered Inputs

- inputs/06-thinking-effort-feedback.md.

## Prerequisites

- SB01-SB06 remain valid for their behavior; their proof did not cover shared thinking.
Source inspection confirms the unconditional source-managed guard and absent catalog
metadata, not a stale Docker image. Entry semantic gate: Pass, 2026-08-27.

## Exact Source References

- repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/AgentThinkingEffortPolicy.cs
- repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderCatalogProjection.cs
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedProviderProfileMapper.cs
- repo://src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderHttpRelayClient.cs
- repo://src/UI/CanDoItAll.Conversations.Components/ConversationProviderModelSelector.razor

## Deliverables

- Typed optional per-model wire metadata, canonical revisions, strict validation and
immutable snapshots. Source derives support from its real provider policy/discovery;
client uses that metadata by routing ID without guessing the underlying model family.
Source validates every explicit request and applies its current default only when
the caller omitted an override. No mutable provider-wide override state.

Main OpenAI suggestions are an explicit allowlist intersected with discovered IDs;
the full catalog and existing selections remain valid. Source publishes suggestion
membership so clients mirror it. Natural ordering uses real display names.

## Dependency Impact

- Abstractions owns protocol data, ProviderManagement maps source/runtime policies,
HTTP owns wire enforcement, Models owns agent policy, existing components render.
No new project references, interfaces, runtime partials or generic service layer.
Simple Chats carries suggestion membership through its existing option/presentation
contracts; execution validates against full published membership, not the shortlist.
Prepared-agent fingerprints include capabilities to prevent stale post-sync reuse.
Live validation reopened this gate for source-owned temperature policy: opaque
client model IDs skipped the standard OpenAI temperature omission, producing real
HTTP 400 responses for Sol High. Publish the source's typed omission flag with
thinking metadata; enforce it in both client MAF options and the source relay.
The named post-checkpoint invalidation is model-parameter mapping, covered by the
failing-first temperature regression, MAF and component policy suites, and rerun
live requests. Preserve the existing evidenced Mini/Luna/Terra tool restriction;
use a separately UI-configured Responses publication to validate those models.
Optional absent metadata remains Unknown and prompts synchronization, never guessed.
Old snapshots keep their canonical revision when optional metadata is absent.

## Validation Depth

- Governed. xUnit/VSTest stable filters: SharedThinkingEffort, SharedProviderProtocol,
SharedProviderRelay, SharedProviderRuntimeProjection, ProviderProfileThinkingCapability,
AgentThinkingEffortSettings and ProviderModelSelector. Freeze --list-tests discovery
before each run; zero fails. Pure policy tests include a failing-first shared override.
CodeAnalytics impact may expand selection at frozen SB07 checkpoint only; public
catalog shape and relay dispatch are the named invalidation keys.

## Acceptance Checklist

- Same model-specific choices and default labels on source/client; unsupported/unknown fail closed.
- Explicit low/high/none/max and invalid values are handled per model without cross-agent leakage.
- Requests without an override inherit the current source default; explicit override wins.
- Metadata and suggestion changes invalidate revisions; missing metadata remains compatible.
- Curated suggestions exclude snapshots/obsolete IDs without losing saved assignments.
- Focused tests/build and architecture review pass; source and browser proof continue in SB08.

## UI Composition

Existing agent/chat dialog Runtime tab, two-column provider/model row and effort field.
No added cards, stats or dialogs. 1920x1080 desktop; dialog body owns vertical scroll;
native dropdown owns its list. First viewport shows model/effort and Save. Inspect
normal and open-dropdown states for readable names and no clipping.

## Boundary Ownership

See Dependency Impact. Typed adapter mappings only; no provider implementations in contracts.

## Dependency Direction

Unchanged. Scoped CodeAnalytics snap-20260827223247-84333f15: healthy, no cycles.
Components MCP returned Transport closed; existing shared controls/source and real
Playwright are the documented fallback. No shared library changes.

## Pattern Decision

Small pure policy and adapter functions; reuse existing source/catalog/driver paths.

## Testability Contract

Pure contract and request-policy tests, component behavior, real materialization tests,
then actual UI-configured multi-agent upstream requests in SB08. No stub counts as live proof.

## Partial Class Policy

No new partials. Existing Razor ownership retained.

## Proof Required

- proof/SB07 manifest, invariants, pre/post hashes, red/green transcripts, source audit.

## Architecture Proof Required

Exact diff review, dependency evidence, isolated policy tests; no project changes.

## Progression Gate

- SB08 may begin after focused checks. Reopen for unknown capability inference, lost
override, rejected valid source default, invented models or selected legacy model loss.

Final gate: Pass for owned behavior. 308 final focused cases pass with exact discovery;
live-discovered temperature, SDK envelope and Responses terminal defects were repaired
and revalidated. Broader checkpoint failures remain explicit, not a whole-repo pass.
See proof/SB07/manifest.md and reviews/03-thinking-final-verifier.md.
