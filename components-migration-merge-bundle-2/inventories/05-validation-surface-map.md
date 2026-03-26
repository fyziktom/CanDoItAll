# Validation Surface Map

## Key Zyphonote Pages To Validate

| Page | Why it matters | Component families covered |
| --- | --- | --- |
| `C:\repositories\Zyphonote\src\App.Blazor\Pages\AccountMarketplace.razor` | highest concentration of badge, toolbar, list, card, and fact-table patterns | badges, list items, toolbars, card actions, fact tables, page shells |
| `C:\repositories\Zyphonote\src\App.Blazor\Pages\AccountMyScores.razor` | heavy use of sheet-card primitives and action surfaces | cards, chip and badge family, meta lists, card actions, headings |
| `C:\repositories\Zyphonote\src\App.Blazor\Pages\AccountSellerProfile.razor` | profile field and toggle wrappers should collapse into shared form primitives cleanly | form shells, settings and toggle surfaces, tag editor, sheet cards |
| `C:\repositories\Zyphonote\src\App.Blazor\Pages\AccountDashboard.razor` | catches stats and dashboard card patterns | metric cards, action cards, page shells |
| `C:\repositories\Zyphonote\src\App.Blazor\Pages\AccountLogin.razor` | validates auth-card replacement and modal/page-shell alignment | auth card, text, button, form section |
| `C:\repositories\Zyphonote\src\App.Blazor\Pages\LegalPrivacy.razor` | validates legal navigation and document-shell components | legal TOC and nav, headings, page shell, divider |
| `C:\repositories\Zyphonote\src\App.Blazor\Pages\AccountEvents.razor` | validates workspace shell and modal parity around planning flows | page header parity, workspace panels, modal shell |

## Key Ownership Checks

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib` should contain the promoted shared families in explicit subfolders.
- `C:\repositories\Zyphonote\src\Zyphonote.Components\Zyphonote.Components.csproj` should no longer use wildcard-linked ownership.
- `C:\repositories\Zyphonote\src\App.Components` should no longer provide long-term shared wrappers once consumer migration is complete.

## Regression Questions

- do badge, chip, and pill surfaces still render correctly in marketplace, scores, and profile pages
- do shared card shells still accept app-local classes for marketplace and seller-profile variants
- do toolbar and filter layouts still collapse correctly on narrow widths
- does the shared modal host preserve keyboard focus, backdrop close rules, and header/footer slots
- has any Zyphonote-only CSS leaked into `BaseLib`
