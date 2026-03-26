# Bundle 2 Compatibility Debt

Date: 2026-03-26

## Shim

- name: `ZyWorkspaceModal`
- location: `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Modals\Compatibility\ZyWorkspaceModal.razor`
- introduced for: preserving existing account-page modal call sites while moving runtime modal ownership to the shared `Dialog` host

## Temporary Justification

- renaming every `ZyWorkspaceModal` usage to a neutral shared name in the same wave would add page churn without changing modal behavior

## Required Removal Condition

- account pages are switched to `Dialog` or another neutral shared modal name and no consumer still depends on the `Zy*` compatibility alias

## Owner

- responsible subbundle: `04-forms-toolbars-modals-and-interactive-primitives`
- follow-up task: remove the shared `ZyWorkspaceModal` alias after page call sites are normalized

## Shim

- name: `StatusChip`
- location: `C:\repositories\Zyphonote\src\Zyphonote.Components\Components\Compatibility\StatusChip.razor`
- introduced for: preserving app-facing `App.Blazor.Components.StatusChip` usage on home, editor, MIDI, and drums pages after wildcard linkage removal

## Temporary Justification

- those pages still use the old API shape and were outside the main shared-surface migration path for bundle 2

## Required Removal Condition

- the remaining `StatusChip` call sites are replaced with `Badge`, `Chip`, or a domain-specific local status surface and no page imports the compatibility component

## Owner

- responsible subbundle: `05-zyphonote-consumer-collapse-and-local-cleanup`
- follow-up task: migrate the remaining status-chip pages and delete the compatibility component

## Shim

- name: `UiButton`, `UiButtonKind`, `UiCard`, `UiField`, `UiSection`
- location:
  - `C:\repositories\Zyphonote\src\Zyphonote.Components\AppComponents\UiButton.razor`
  - `C:\repositories\Zyphonote\src\Zyphonote.Components\AppComponents\UiButtonKind.cs`
  - `C:\repositories\Zyphonote\src\Zyphonote.Components\AppComponents\UiCard.razor`
  - `C:\repositories\Zyphonote\src\Zyphonote.Components\AppComponents\UiField.razor`
  - `C:\repositories\Zyphonote\src\Zyphonote.Components\AppComponents\UiSection.razor`
- introduced for: keeping `App.AI.TranscriptionLab` compiling after `Zyphonote.Components.csproj` stopped linking `App.Components`

## Temporary Justification

- transcription-lab still imports `App.Components` and was not rewritten onto shared primitives in this bundle wave

## Required Removal Condition

- `App.AI.TranscriptionLab` is migrated off the `App.Components` API surface and the dead source under `C:\repositories\Zyphonote\src\App.Components\Ui*` is no longer needed for compatibility review

## Owner

- responsible subbundle: `05-zyphonote-consumer-collapse-and-local-cleanup`
- follow-up task: replace transcription-lab `Ui*` usage with `BaseLib` primitives and delete the temporary `AppComponents` shim layer
