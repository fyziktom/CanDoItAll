# Explicit Model Override Canonicity

## Status

- Status: `Completed`

## Objective

Make the agent Runtime tab preserve explicit model override intent through save and reload, while retaining empty model as the canonical provider-default-linked state.

## Covered Inputs

- Follow-up report that checking override and saving says success, then the dialog returns to unselected.
- Requirement R004 for the override checkbox/text field.
- Requirement R007 for explicit override canonicity.

## Prerequisites

- Subbundle 01 shared selector behavior exists.
- Subbundle 02 agent Runtime tab integration exists.
- Existing provider-default linkage remains represented by an empty `AgentDefinition.Model`.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ProviderModelSelector.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentDetailsDialog.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Catalog\AgentFrameworkWorkspaceCatalogService.Agents.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProviderModelSelectorTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\AiAgentsPageTests.cs`

## Deliverables

- Explicit override state is inferred from canonical data: empty model means provider default; non-empty model means explicit model override, even when the text equals the provider default.
- Catalog save no longer rewrites a non-empty model equal to provider default into empty.
- Agent details save/reload tests cover both provider-default linkage and explicit override persistence.
- Selector tests cover the provider-default-equal explicit model case.

## Dependency Impact

- This subbundle repairs the source-of-truth rule used by the Agent Details Runtime tab.
- If this fails, provider-default UI proof from subbundle 02 is incomplete because it cannot distinguish linked default from explicit override.

## Validation Depth

- Failing-first targeted component tests for the save/reload regression.
- Passing component tests for `ProviderModelSelector` and `AgentDetails_runtime`.
- Source assertion that production save and selector logic preserve non-empty model strings.
- Browser proof for the Runtime tab when feasible; explicit blocker if the local app cannot start.

## Implementation Steps

- Add failing tests that reproduce an explicit override of the provider default reloading unchecked.
- Change selector external-value interpretation so empty means provider default and non-empty values remain explicit overrides unless chosen through the dropdown.
- Change agent save normalization so it trims model text without collapsing provider-default-equal non-empty strings to empty.
- Keep dropdown provider-default selection emitting empty.
- Run targeted tests, source assertion, anti-stub audit, and browser proof or blocker.

## Do Not Do

- Do not introduce a second agent source of truth outside `AgentDefinition.Model` for this repair.
- Do not break provider-default linkage for empty model values.
- Do not change workflow persistence semantics in this subbundle.

## Acceptance Checklist

- Saving provider default through the dropdown stores empty model and reopens override unchecked.
- Saving override with a custom model stores the model and reopens override checked.
- Saving override with text equal to provider default stores that concrete model and reopens override checked.
- Catalog save preserves any non-empty trimmed model string.
- Provider changes still clear stale model values.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --configuration Release --no-restore --filter "ProviderModelSelector|AgentDetails_runtime" --logger "console;verbosity=normal"`
- Source assertion transcript for selector/save canonicity.
- Anti-stub audit transcript for production `TODO`, `NotImplemented`, and fixture-specific branching in touched production files.
- Browser proof for `/agents?tab=agents` Runtime tab at desktop viewport, or an explicit environment blocker.
- `proof/SB03/manifest.md` with changed-file hashes and transcript paths.

## Browser Validation Logging

- Route: `/agents?tab=agents`.
- Viewport: desktop first.
- Actions: open an existing agent dialog, open Runtime tab, enable override, save a concrete model, reopen the dialog.
- Assertions: override checkbox remains selected, text field is present and populated, provider-default dropdown path still reopens unchecked.

## Progression Gate

- Pass only if tests prove explicit override and provider-default linkage are both canonical after save/reload, and proof artifacts exist under `proof/SB03/`.

## Suggested Agent Prompt

Repair subbundle 03. Add a failing test for explicit model override equal to provider default reloading unchecked, then update selector/catalog save canonicity so empty model means provider-default linkage and any non-empty model means explicit override. Keep prior provider-default dropdown behavior passing, run targeted tests, record source and anti-stub proof, and update `proof/SB03/manifest.md`.

## Closure Notes

- Completed on 2026-05-20.
- `ProviderModelSelector` now treats empty as the provider-default choice in agent mode, while non-empty model values remain explicit overrides even when they equal the provider default text.
- `AgentFrameworkWorkspaceCatalogService.SaveAgentAsync` now preserves non-empty model strings and only trims whitespace; provider-default linkage is still selected by the dropdown emitting an empty model.
- Browser proof was attempted but blocked by managed app startup `HealthTimeout`; the timed-out app session was stopped.

## Proof Captured

- Failing-first: `proof/SB03/transcripts/failing-first-explicit-override.txt`.
- Build: `proof/SB03/transcripts/passing-build.txt`.
- Passing tests: `proof/SB03/transcripts/passing-targeted-tests.txt`.
- Source assertions: `proof/SB03/transcripts/source-assertions.txt`.
- Anti-stub audit: `proof/SB03/transcripts/anti-stub-audit.txt`.
- Browser blocker: `proof/SB03/transcripts/browser-proof-blocker.txt`.
- Manifest: `proof/SB03/manifest.md`.
