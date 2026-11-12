using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CanDoItAll.AppComponents;

public sealed class AppToolbarState(IJSRuntime js) : IPageHeaderOutlet {
    public const string HelpVisibleCookieName = "candoitall.app-toolbar.help-visible";
    public const string StatsVisibleCookieName = "candoitall.app-toolbar.stats-visible";
    private object? tabTextOwner;
    private object? helpOwner;
    private object? actionsOwner;
    private object? statsOwner;

    public string? TabText { get; private set; }
    public string? HelpHeading { get; private set; }
    public RenderFragment? HelpContent { get; private set; }
    public RenderFragment? ActionsContent { get; private set; }
    public RenderFragment? StatsContent { get; private set; }
    public bool HelpVisible { get; private set; } = true;
    public bool StatsVisible { get; private set; } = true;
    public event Action? Changed;

    public void InitializeFromCookies(string? helpVisibleCookie, string? statsVisibleCookie) {
        HelpVisible = helpVisibleCookie is null || string.Equals(helpVisibleCookie, "true", StringComparison.Ordinal);
        StatsVisible = statsVisibleCookie is null || string.Equals(statsVisibleCookie, "true", StringComparison.Ordinal);
    }

    public void Register(object owner, PageHeaderPresentation presentation) {
        helpOwner = owner;
        actionsOwner = owner;
        statsOwner = owner;
        HelpHeading = presentation.Title;
        HelpContent = string.IsNullOrWhiteSpace(presentation.Description)
            ? null
            : builder => builder.AddContent(0, presentation.Description);
        ActionsContent = presentation.Actions;
        StatsContent = presentation.Stats;
        Changed?.Invoke();
    }

    public void Clear(object owner) {
        var changed = false;
        if (ReferenceEquals(helpOwner, owner)) {
            helpOwner = null;
            HelpHeading = null;
            HelpContent = null;
            changed = true;
        }
        if (ReferenceEquals(actionsOwner, owner)) {
            actionsOwner = null;
            ActionsContent = null;
            changed = true;
        }
        if (ReferenceEquals(statsOwner, owner)) {
            statsOwner = null;
            StatsContent = null;
            changed = true;
        }
        if (changed) {
            Changed?.Invoke();
        }
    }

    public void SetTabText(object owner, string? tabText) {
        tabTextOwner = owner;
        if (string.Equals(TabText, tabText, StringComparison.Ordinal)) {
            return;
        }
        TabText = tabText;
        Changed?.Invoke();
    }

    public void ClearTabText(object owner) {
        if (!ReferenceEquals(tabTextOwner, owner)) {
            return;
        }
        tabTextOwner = null;
        TabText = null;
        Changed?.Invoke();
    }

    public void SetHelp(object owner, string? heading, RenderFragment? content) {
        helpOwner = owner;
        HelpHeading = heading;
        HelpContent = content;
        Changed?.Invoke();
    }

    public void SetStats(object owner, RenderFragment? content) {
        statsOwner = owner;
        StatsContent = content;
        Changed?.Invoke();
    }

    public void SetActions(object owner, RenderFragment? content) {
        actionsOwner = owner;
        ActionsContent = content;
        Changed?.Invoke();
    }

    public async Task SetHelpVisibleAsync(bool visible) {
        await js.InvokeVoidAsync("CanDoItAll.browserState.saveCookie", HelpVisibleCookieName, visible ? "true" : "false");
        HelpVisible = visible;
        Changed?.Invoke();
    }

    public async Task SetStatsVisibleAsync(bool visible) {
        await js.InvokeVoidAsync("CanDoItAll.browserState.saveCookie", StatsVisibleCookieName, visible ? "true" : "false");
        StatsVisible = visible;
        Changed?.Invoke();
    }
}
