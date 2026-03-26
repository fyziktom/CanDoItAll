# Sharedization Matrix

## Legend

| Action | Meaning |
| --- | --- |
| `ExpandExistingBaseLib` | add parity to a shared primitive that already exists |
| `PromoteNewBaseLib` | create a new shared primitive or support type |
| `MergeIntoExistingBaseLib` | extend an existing shared primitive; do not copy the wrapper 1:1 |
| `RetireWrapper` | remove the Zyphonote wrapper and use shared primitives directly |
| `KeepLocal` | keep inside Zyphonote because the behavior is domain-specific |
| `MoveFeatureLocal` | keep out of `Zyphonote.Components`; place it near the owning workflow |

## Already In `BaseLib`, But Parity Is Incomplete

| Source | Target | Action | Notes |
| --- | --- | --- | --- |
| `EmptyState` | `BaseLib\Feedback\EmptyState` | `ExpandExistingBaseLib` | current `BaseLib` version is richer, but consumer replacement still needs explicit compatibility guidance for workspace and page-shell usage |
| `PageHeader`, `PageHeaderActions`, `PageHeaderCopy` | `BaseLib\Navigation\PageHeader` | `ExpandExistingBaseLib` | keep one strong shared `PageHeader` with slots; do not keep three permanent Zyphonote wrappers |
| `ZyNotificationHost` | `BaseLib\Feedback\Notification` | `ExpandExistingBaseLib` | current `BaseLib` notification host is too thin if Zyphonote needs positioned, dismissible toasts |
| `ZyWorkspaceModal` | `BaseLib\Modals\Dialog` or `ModalShell` | `ExpandExistingBaseLib` | current `Dialog.razor` is only a stub; shared modal behavior is missing |

## Promote Or Merge Into `BaseLib`

### Badges, Chips, And Status Surfaces

| Source | Target | Action | Notes |
| --- | --- | --- | --- |
| `Badge`, `BadgesGroup`, `StatusChip`, `Pill`, `PillList`, `Chip`, `ChipRow`, `ProfileTagChip`, `ProfileTagChipRow` | `BaseLib\Badges` family | `PromoteNewBaseLib` plus `MergeIntoExistingBaseLib` | use typed tone and shape enums; unify link, button, and text rendering; do not keep separate Zyphonote badge dialects |
| `ComponentIsolationEnums.cs` (`BadgeTone`, `PillTone`) | family-local support types in `BaseLib\Badges` | `PromoteNewBaseLib` | do not keep these enums in a global junk-drawer file |

### Typography, Headings, And Structural Text

| Source | Target | Action | Notes |
| --- | --- | --- | --- |
| `Eyebrow`, `SmallText`, `MonoText`, `MutedInline`, `FooterText` | `BaseLib\Typography\TextBlock` | `MergeIntoExistingBaseLib` | `TextBlock` already has `TextStyle` support for these concepts; prefer expanding that API or adding thin shared wrappers only if readability justifies it |
| `SectionHead`, `SectionHeading` | `BaseLib\Typography` and `BaseLib\Navigation` | `MergeIntoExistingBaseLib` | either a true heading wrapper survives or pages use `TextBlock` and `Stack`; do not keep both Zyphonote names permanently |
| `Divider` | `BaseLib\Layout\Divider` | `PromoteNewBaseLib` | keep it tiny and shared rather than re-creating it per app |

### Identity And Simple Media

| Source | Target | Action | Notes |
| --- | --- | --- | --- |
| `Avatar`, `CreatorAvatar` | `BaseLib\Identity\Avatar` | `PromoteNewBaseLib` | one avatar primitive with size and shape variants is enough |
| `CreatorLine`, `CreatorSocialLink` | `BaseLib\Identity` | `PromoteNewBaseLib` or `RetireWrapper` | keep only if the composition is truly reused; otherwise pages can compose `Avatar`, `TextBlock`, and anchor elements directly |

### Cards, Surfaces, Stats, And Shells

| Source | Target | Action | Notes |
| --- | --- | --- | --- |
| `ActionCard`, `AuthCard`, `PanelCard`, `HeroCard`, `ParitySectionCard`, `SheetCard` | `BaseLib\Cards` family | `MergeIntoExistingBaseLib` | do not create five nearly identical shared card wrappers if a typed card appearance model will cover them |
| `SheetCardTop`, `SheetCardHeading`, `CardActions`, `CardButton`, `CardGrid` | `BaseLib\Cards` family | `PromoteNewBaseLib` plus `MergeIntoExistingBaseLib` | these are reusable card slots and actions, not Zyphonote-only concepts |
| `PageShell`, `WorkspacePanel`, `WorkspaceSplit`, `SheetGrid`, `SheetSection`, `SheetNote` | `BaseLib\Layout` and `BaseLib\Cards` | `PromoteNewBaseLib` plus `MergeIntoExistingBaseLib` | unify page-shell and workspace surface patterns; stop tying them to `sheet` or `workspace` CSS names |
| `StatBox`, `StatsCardRow`, `StatsGrid`, `BuilderStatBox`, `BuilderStatStrip`, `CardStatsWithNumber`, `PriceBar`, `PriceRow` | `BaseLib\Cards` or `BaseLib\DataDisplay` metric family | `PromoteNewBaseLib` plus `RetireWrapper` | shared metric surfaces are valuable, but rename them generically and use typed size/appearance enums instead of strings |
| `ComponentIsolationEnums.cs` (`WorkspacePanelTone`, `PriceBarTone`) | colocated support types in the owning family | `PromoteNewBaseLib` | keep support types near the shared family, not in Zyphonote |

### Lists, Definition Displays, And Document Nav

| Source | Target | Action | Notes |
| --- | --- | --- | --- |
| `FactTable`, `ListGroup`, `ListItem`, `MetaList`, `PlainList` | `BaseLib\Lists` and `BaseLib\DataDisplay` | `PromoteNewBaseLib` plus `MergeIntoExistingBaseLib` | base structure is generic; keep app-specific list-item refinements in page-local CSS |
| `LegalToc`, `LegalTocNav` | `BaseLib\Lists` or `BaseLib\Navigation` | `PromoteNewBaseLib` | these are generic documentation and legal navigation primitives |

### Forms, Toolbars, Modals, And Interactive Primitives

| Source | Target | Action | Notes |
| --- | --- | --- | --- |
| `FormRow`, `FormStack`, `InlineActions` | `BaseLib\Forms` and `BaseLib\Layout` | `PromoteNewBaseLib` or `RetireWrapper` | some may collapse into `Stack` and `Row`; do not keep them app-local if shared spacing semantics are useful |
| `SheetField`, `ProfileField`, `ProfileToggle`, `SettingsSwitchLabel`, `SettingsSwitchRow`, `DebugToggle` | `BaseLib\Forms` | `MergeIntoExistingBaseLib` plus `RetireWrapper` | the shared owner should model the reusable form shell; app-specific class names do not belong in the shared API |
| `Toolbar`, `ToolbarActions`, `ToolbarFields`, `ToolbarRow`, `DashboardActions` | `BaseLib\Navigation\FilterBar` and `BaseLib\Layout` | `MergeIntoExistingBaseLib` plus `RetireWrapper` | `FilterBar` already exists; finish it instead of keeping a second toolbar stack in Zyphonote |
| `TagTextEdit`, `TagTextValueNormalizer.cs` | `BaseLib\Forms\TagEditor` | `PromoteNewBaseLib` | this is a real reusable control, not page glue |
| `Callout` | `BaseLib\Feedback\Alert` or `Callout` | `MergeIntoExistingBaseLib` | do not keep both `Alert` and `Callout` if one well-shaped shared primitive can cover the behavior |
| `ImmersiveRibbonTabs` | `BaseLib\Navigation\RibbonTabs` | `PromoteNewBaseLib` | this is generic and was under-classified in bundle 1 |
| `ComponentIsolationEnums.cs` (`CalloutTone`) | support type in the owning family | `PromoteNewBaseLib` | colocate it with the promoted shared component |

### App.Components Wrapper Retirement

| Source | Target | Action | Notes |
| --- | --- | --- | --- |
| `UiButton`, `UiButtonKind` | `BaseLib\Buttons\Button` | `RetireWrapper` | do not keep a second shared button abstraction in Zyphonote |
| `UiCard` | `BaseLib\Cards\Card` | `RetireWrapper` | delete once consumers are migrated |
| `UiField` | `BaseLib\Forms\FormField` | `RetireWrapper` | delete once consumers are migrated |
| `UiSection` | `BaseLib\Forms\FormSection` or `BaseLib\Cards` | `RetireWrapper` | keep only if a real app-specific semantic wrapper survives |

## Keep In Zyphonote Or Move Feature-Local

### Keep Local: Domain-Specific Or Canvas-Specific

| Source | Target | Action | Notes |
| --- | --- | --- | --- |
| `BoughtLibraryCardsList`, `LearningBuilderPackageCardsList`, `LearningPackageStudyHeaderCards`, `LearningPackageStudySidebarCards`, `MarketplaceListingsGrid`, `OwnedScoreCardsList`, `OwnedScorePickerModal`, `PlaylistOverviewCardsList`, `CatalogCardPreview` | `Zyphonote.Components` or feature-local Zyphonote folders | `KeepLocal` | domain compositions should consume shared primitives, not become shared primitives |
| `ChordInput`, `IntervalInput`, `NoteInput`, `QuickChordInput`, `QuickIntervalInput`, `QuickNoteInput`, `MidiInputKeyboard`, `MidiLiveInputStatus`, `NotationEditor`, `ResultPanel`, `KeyboardKeySvg`, `KeyboardOctaveSvg`, `KeyboardSvg`, `LeadSheetSvg`, `StaffClefSvg`, `StaffSvg` | Zyphonote domain UI | `KeepLocal` | these are music-specific or theory-specific |
| `PlanningEventsCalendar`, `RepositoryGraphCanvas`, `ScoreCreationWizard`, `ScoreRepositoryWorkbench` | Zyphonote feature or future specialized library | `KeepLocal` | not `BaseLib` work; too tied to Zyphonote JS or workflow contracts |

### Move Feature-Local: Do Not Keep In `Zyphonote.Components`

| Source | Target | Action | Notes |
| --- | --- | --- | --- |
| `ScoreWorkbenchBranchRow`, `ScoreWorkbenchField`, `ScoreWorkbenchForm`, `ScoreWorkbenchGrid`, `ScoreWorkbenchItem`, `ScoreWorkbenchItemTop`, `ScoreWorkbenchList` | folders near score-workbench workflows in `App.Blazor` | `MoveFeatureLocal` | they are one-class workflow wrappers, not shared library assets |

## Matrix Outcome

Bundle 2 intentionally moves beyond bundle 1's conservative classification:

- more components now qualify for `BaseLib` because their actual code is generic
- several Zyphonote wrappers should be deleted instead of being preserved
- `Zyphonote.Components` should become much smaller, explicit, and domain-focused
