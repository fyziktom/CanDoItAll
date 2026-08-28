# SB10: Source and both client model thinking UI proof

## Status

- Status: Completed
- Proof tier: Governed

## Objective

Close R13/N013 and R14/N014 through actual UI on 5210, 5212 and especially 5214.

## Covered Inputs

- inputs/07-provider-model-thinking-settings-feedback.md

## Prerequisites

- SB09 tests/build and architecture review. Real OpenAI/Ollama access and existing
source credentials. No data reset or unrelated permissions changes authorized.

## Exact Source References

- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/SharedProviderSourcesDialog.razor.cs

## Deliverables

- Rebuilt three apps, data intact. Source UI default/custom/reset configuration,
client synchronization, different-model dropdown assertions, save/reopen and real
request evidence for supported choices. Record upstream errors without masking them.

## Dependency Impact

- Consumes SB09 persisted source configuration and normal shared catalog sync.
No substitute fixture catalogs or invented OpenAI names; no source credentials on client.

## Validation Depth

- Governed. Playwright MCP at 1920x1080, exact option text/value assertions, screenshots
and real source usage. Focused tests remain SB09-owned. Docker build/health are
required host checks, not excuses for unfiltered suites. Expected UI discovery:
known Sol, another supported model, unsupported model and custom Ollama configuration.
Invalidate proof after image/configuration/model metadata changes.

## Acceptance Checklist

- 5214 stale snapshot is refreshed through UI without losing agent draft.
- Source automatic versus administrator provenance is visible and survives reload.
- Both clients show exactly suitable model-specific choices and read-only source settings.
- Independent agent efforts execute with complete source usage where upstream supports them.
- All three healthy, same final image; old test history/data preserved.

## UI Composition

Same desktop/scroll policy as SB09. Inspect provider normal table and edit dialog,
shared read-only table, agent dropdown open and saved/reopened state. No mobile scope.

## Boundary Ownership

Existing UI/application/provider boundaries; proof uses normal production producers.

## Dependency Direction

No changes; verify SB09 contract in actual host composition.

## Pattern Decision

Existing UI and Docker deployment scripts, no alternate execution path.

## Testability Contract

Real positive and negative UI assertions, downstream source usage and final semantic review.
The primary agent performed the source-backed re-review; no independent reviewer is claimed.

## Partial Class Policy

No production changes planned here; reopen SB09 if defects are found.

## Proof Required

- proof/SB10/manifest.md, semantic invariants, UI/host transcripts, inspected screenshots
and final verifier. Red evidence is the captured pre-fix 5214 UI from SB09.

## Architecture Proof Required

Verify actual runtime respects source policy and no direct upstream credentials leaked.

## Progression Gate

- Close only after actual UI and host evidence for both clients. Missing real proof
is explicit unfinished work, not a risk footnote.
