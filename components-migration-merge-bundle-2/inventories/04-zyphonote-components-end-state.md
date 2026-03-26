# Zyphonote.Components End State

## Current Problem

`C:\repositories\Zyphonote\src\Zyphonote.Components\Zyphonote.Components.csproj` currently owns its component surface by linking almost everything under `..\App.Blazor\Components`.

That makes it hard to answer basic ownership questions:

- which components are truly library assets
- which components are page-local
- which components should have been shared already
- which components are still carrying migration debt

## Target Ownership Model

### `CanDoItAll.Components.BaseLib`

Owns:

- all reusable app-agnostic primitives and shells identified in the sharedization matrix

### `Zyphonote.Components`

Owns only:

- domain-specific reusable Zyphonote UI
- music, notation, MIDI, and workflow-specific reusable surfaces
- limited app-specific compositions that are genuinely reused across Zyphonote features

### `App.Blazor`

Owns:

- page-local workflow wrappers
- one-off list and form shells that only exist to arrange shared primitives inside a single workflow
- score-workbench local wrappers if they survive at all

## Required Project File Correction

Replace the current wildcard ownership model with explicit includes.

Do not keep:

- `..\App.Blazor\Components\**\*.razor` as a default include
- a negative remove list as the main ownership mechanism

Do keep:

- explicit `Compile`, `Content`, and `None` includes for files physically owned by `Zyphonote.Components`
- a folder tree under `C:\repositories\Zyphonote\src\Zyphonote.Components\Components\...`

## Expected Residual `Zyphonote.Components` Surface

This should be the rough shape after bundle-2 implementation settles:

- `Commerce`
  - `BoughtLibraryCardsList`
  - `MarketplaceListingsGrid`
  - `OwnedScoreCardsList`
  - `OwnedScorePickerModal`
  - `PlaylistOverviewCardsList`
  - `CatalogCardPreview`
- `Learning`
  - `LearningBuilderPackageCardsList`
  - `LearningPackageStudyHeaderCards`
  - `LearningPackageStudySidebarCards`
- `Music`
  - `ChordInput`
  - `IntervalInput`
  - `NoteInput`
  - `QuickChordInput`
  - `QuickIntervalInput`
  - `QuickNoteInput`
  - `MidiInputKeyboard`
  - `MidiLiveInputStatus`
  - `NotationEditor`
  - `ResultPanel`
  - `KeyboardKeySvg`
  - `KeyboardOctaveSvg`
  - `KeyboardSvg`
  - `LeadSheetSvg`
  - `StaffClefSvg`
  - `StaffSvg`
- `Canvas` or `Workflows`
  - `PlanningEventsCalendar`
  - `RepositoryGraphCanvas`
  - `ScoreCreationWizard`
  - `ScoreRepositoryWorkbench`

## Components That Should Leave `Zyphonote.Components`

- all `Ui*` wrappers from `App.Components`
- all migrated badge, list, card, shell, text, toolbar, modal, and form primitives
- score-workbench one-class wrappers that are not true library assets

## Compatibility Wrapper Rule

Compatibility wrappers inside `Zyphonote.Components` are allowed only when they:

- reduce churn materially
- point at `BaseLib` primitives
- have an explicit removal step in the same migration wave

They are not allowed to become permanent.
