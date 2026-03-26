# Bundle 2 Proof Ledger

Date: 2026-03-26

## Build Proof

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx`
  - result: success
  - errors: 0
  - warnings: 4 existing `NU1510` warnings in `CanDoItAll.Mcp.DotNetWatch`
- `dotnet build C:\repositories\Zyphonote\Zyphonote.slnx`
  - result: success
  - errors: 0
  - warnings: 0
- focused verification builds also passed:
  - `C:\repositories\Zyphonote\src\App.Blazor\Zyphonote.App.csproj`
  - `C:\repositories\Zyphonote\tests\App.Web.PlaywrightTests\Zyphonote.App.PlaywrightTests.csproj`

## Shared Ownership Proof

- `CanDoItAll.Components.BaseLib` now groups shared surface families under:
  - `Badges`
  - `Buttons`
  - `Cards`
  - `DataVisualization`
  - `Feedback`
  - `Forms`
  - `Identity`
  - `Infrastructure`
  - `Layout`
  - `Lists`
  - `Modals`
  - `Navigation`
  - `Typography`
- `BaseLibPrimitives.cs` was retired and support types were split into family-local files.
- `C:\repositories\Zyphonote\src\Zyphonote.Components\Zyphonote.Components.csproj` no longer links:
  - `..\App.Blazor\Components\**\*.razor`
  - `..\App.Components\Ui*.razor`
  - remove-list ownership corrections for linked app components
- compiled shared modal ownership now resolves through `CanDoItAll.Components.BaseLib`:
  - removed `C:\repositories\Zyphonote\src\App.Blazor\Components\ZyWorkspaceModal.razor`
  - account pages now bind to the shared modal host instead of a local shadow implementation
- current compiled `Zyphonote.Components` surface is explicitly limited to:
  - `Components\Commerce\BoughtLibraryCardsList.razor`
  - `Components\Commerce\CatalogCardPreview.razor`
  - `Components\Commerce\MarketplaceListingsGrid.razor`
  - `Components\Commerce\OwnedScoreCardsList.razor`
  - `Components\Commerce\OwnedScorePickerModal.razor`
  - `Components\Commerce\PlaylistOverviewCardsList.razor`
  - `Components\Compatibility\EmptyState.razor`
  - `Components\Compatibility\PageHeader.razor`
  - `Components\Compatibility\StatusChip.razor`
  - `Components\Music\ChordInput.razor`
  - `Components\Music\IntervalInput.razor`
  - `Components\Music\KeyboardKeySvg.razor`
  - `Components\Music\KeyboardOctaveSvg.razor`
  - `Components\Music\KeyboardSvg.razor`
  - `Components\Music\LeadSheetSvg.razor`
  - `Components\Music\NoteInput.razor`
  - `Components\Music\QuickChordInput.razor`
  - `Components\Music\QuickIntervalInput.razor`
  - `Components\Music\QuickNoteInput.razor`
  - `Components\Music\ResultPanel.razor`
  - `Components\Music\StaffClefSvg.razor`
  - `Components\Music\StaffSvg.razor`
- workflow-local score workbench wrappers are no longer pretending to be reusable library assets in `Zyphonote.Components`:
  - `Components\ScoreWorkbenchBranchRow.razor`
  - `Components\ScoreWorkbenchField.razor`
  - `Components\ScoreWorkbenchForm.razor`
  - `Components\ScoreWorkbenchGrid.razor`
  - `Components\ScoreWorkbenchItem.razor`
  - `Components\ScoreWorkbenchItemTop.razor`
  - `Components\ScoreWorkbenchList.razor`
- `zyphonote-compat.css` ownership did not leak into `BaseLib`
  - verification: no file under `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib` references `zyphonote-compat.css`

## Visual Proof

### Page Surfaces

- login/auth:
  - `C:\repositories\Zyphonote\output\playwright\wasm-repair\appserver-root-login.png`
- legal:
  - `C:\repositories\Zyphonote\output\playwright\wasm-repair\bundle2-legal-privacy.png`
- dashboard:
  - `C:\repositories\Zyphonote\output\playwright\wasm-repair\bundle2-account-dashboard.png`
- my scores:
  - `C:\repositories\Zyphonote\output\playwright\wasm-repair\bundle2-account-my-scores.png`
- seller profile:
  - `C:\repositories\Zyphonote\output\playwright\wasm-repair\bundle2-account-seller-profile.png`
- events:
  - `C:\repositories\Zyphonote\output\playwright\wasm-repair\bundle2-account-events.png`
- marketplace:
  - `C:\repositories\Zyphonote\output\playwright\seller-marketplace-overview-page.png`
  - `C:\repositories\Zyphonote\output\playwright\seller-marketplace-listings-page.png`
- learning builder:
  - `C:\repositories\Zyphonote\output\playwright\seller-learning-builder-page.png`

### Modal And Workspace Surfaces

- seller profile modal:
  - `C:\repositories\Zyphonote\dialogs-repairs\blazor-dashboard-profile-modal.png`
- marketplace modals:
  - `C:\repositories\Zyphonote\output\playwright\seller-marketplace-discount-modal-page.png`
  - `C:\repositories\Zyphonote\dialogs-repairs\blazor-marketplace-activate-confirm-modal.png`
- learning builder modals:
  - `C:\repositories\Zyphonote\dialogs-repairs\blazor-learning-builder-score-preview-modal.png`
  - `C:\repositories\Zyphonote\dialogs-repairs\blazor-learning-builder-score-picker-modal.png`
- my scores modals:
  - `C:\repositories\Zyphonote\dialogs-repairs\blazor-dashboard-create-score-modal.png`
  - `C:\repositories\Zyphonote\dialogs-repairs\blazor-scores-export-modal.png`
  - `C:\repositories\Zyphonote\dialogs-repairs\blazor-dashboard-manage-score-modal.png`
  - `C:\repositories\Zyphonote\dialogs-repairs\blazor-score-playlist-ref-modal.png`

## Validation Test Proof

- `ServerHostRepairSmokeTests.Root_RedirectsToLogin_AndCapturesServerHostScreenshot`
- `ProductFlowsTests.Smoke_LegalRoutes_Load`
- `Bundle2PageProofUiTests.Seller_AccountPages_CaptureBundle2ValidationScreens`
- `AccountModalParityUiTests.Seller_ProfileModal_CaptureAndValidate`
- `AccountModalParityUiTests.Seller_MarketplaceAndBuilderModals_CaptureAndValidate`
- `AccountModalParityUiTests.Seller_MyScoresAndScoreDetailModals_CaptureAndValidate`

## Component Proof Ledger

| Family | Shared target | Consumer pages checked | Build proof | Visual proof | Temporary shims left | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| badges, typography, identity | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Badges`, `Typography`, `Identity` | `AccountMarketplace`, `AccountMyScores`, `AccountSellerProfile`, `AccountDashboard` | both solution builds passed | marketplace, my-scores, seller-profile page and modal captures | yes | typed badge surface kept; `StatusChip` remains temporary in Zyphonote |
| cards, lists, layout, workspace | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Cards`, `Lists`, `Layout` | `AccountMarketplace`, `AccountMyScores`, `AccountEvents`, `AccountDashboard` | both solution builds passed | marketplace overview/listings, account dashboard, account events, manage-score modal | yes | score-workbench wrappers remain feature-local in `App.Blazor` |
| forms, toolbars, modals, navigation | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms`, `Modals`, `Navigation` | `AccountSellerProfile`, `AccountMyScores`, `AccountMarketplace`, `LegalPrivacy` | both solution builds passed | seller-profile modal, create/export/manage-score modals, marketplace modal page, legal privacy capture | yes | legacy local modal shadow removed from `App.Blazor` |
| consumer cleanup and ownership | `C:\repositories\Zyphonote\src\Zyphonote.Components` explicit local surface | `AccountMarketplace`, `AccountMyScores`, `AccountSellerProfile`, `AccountEvents` | `Zyphonote.slnx` passed | page captures plus modal captures | yes | wildcard ownership removed; residual debt documented separately |
