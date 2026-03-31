# Custom CSS Migration Candidates

## Highest-Priority Candidates

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor.css`
- Likely contains broad reusable shells and cards that should move into shared Tailwind classes or BaseLib components before page-specific remnants are left behind.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\Tabs.razor.css`
- Needs explicit review because tabs are already a shared primitive and should not keep drifting from the shared Tailwind component layer.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\ReconnectModal.razor.css`
- Candidate for modal and feedback shared classes if behavior remains identical.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\HelpPopover.razor.css`
- Candidate for shared overlay and help affordance classes, but layering and clipping must be browser-validated carefully.

## Lower-Priority Or Conditional Candidates

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\wwwroot\sandbox.css`
- Safe only for non-canvas catalog surfaces. Canvas previews remain excluded.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\wwwroot\app.css`
- Small enough that only genuinely shared global rules should remain here after migration.
