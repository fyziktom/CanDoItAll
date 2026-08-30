using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CanDoItAll.AppComponents;

/// <summary>
/// Per-circuit channel a routed page uses to register its tab-breadcrumb text, trailing action
/// buttons, help content, and stats content with the shell's single <c>AppToolbar</c> instance,
/// instead of each page declaring its own toolbar/help/stats markup. Pages register via
/// <c>AppToolbarActions</c>/<c>AppToolbarHelp</c>/<c>AppToolbarStats</c> (which clear their own
/// registration on dispose) and call <see cref="SetTabText"/> directly for the plain-string breadcrumb
/// tab segment.
///
/// <see cref="HelpVisible"/>/<see cref="StatsVisible"/> are a single app-wide user preference, not
/// per-page state, persisted as cookies (not localStorage) specifically so the shell can read the
/// correct value synchronously from <c>HttpContext</c> during prerender/first render — a localStorage
/// read requires a JS round-trip that only completes after first paint, which visibly flashes the
/// default (visible) state before collapsing to "hidden" on reload. Reading a cookie has no such gap.
/// Writing still goes through JS (<see cref="SetHelpVisibleAsync"/>/<see cref="SetStatsVisibleAsync"/>),
/// since there is no active HTTP response to attach a Set-Cookie header to once the circuit is running.
/// Per-page content fields (<see cref="TabText"/>, <see cref="HelpContent"/>, <see cref="StatsContent"/>,
/// <see cref="ActionsContent"/>) are cleared solely by the registering portal component's own
/// <c>Dispose()</c> when a page navigates away — there is no separate reset step, so a page transition
/// clears and repopulates the toolbar within a single render rather than visibly flashing empty first.
/// </summary>
public sealed class AppToolbarState(IJSRuntime js)
{
    public const string HelpVisibleCookieName = "candoitall.app-toolbar.help-visible";
    public const string StatsVisibleCookieName = "candoitall.app-toolbar.stats-visible";

    public string? TabText { get; private set; }

    public string? HelpHeading { get; private set; }

    public RenderFragment? HelpContent { get; private set; }

    public RenderFragment? ActionsContent { get; private set; }

    public RenderFragment? StatsContent { get; private set; }

    public bool HelpVisible { get; private set; } = true;

    public bool StatsVisible { get; private set; } = true;

    public event Action? Changed;

    /// <summary>
    /// Applies the persisted visibility preference from cookie values the shell read synchronously off
    /// <c>HttpContext.Request.Cookies</c> during <c>OnInitialized</c> (available during prerender and the
    /// first interactive render, before the circuit becomes persistent). A missing cookie defaults to
    /// visible. Call this from <c>OnInitialized</c>, not <c>OnAfterRenderAsync</c> — it must land before
    /// the first render to avoid any visible flash.
    /// </summary>
    public void InitializeFromCookies(string? helpVisibleCookie, string? statsVisibleCookie)
    {
        HelpVisible = helpVisibleCookie is null || string.Equals(helpVisibleCookie, "true", StringComparison.Ordinal);
        StatsVisible = statsVisibleCookie is null || string.Equals(statsVisibleCookie, "true", StringComparison.Ordinal);
    }

    public void SetTabText(string? tabText)
    {
        if (string.Equals(TabText, tabText, StringComparison.Ordinal))
        {
            return;
        }

        TabText = tabText;
        Changed?.Invoke();
    }

    public void SetHelp(string? heading, RenderFragment? content)
    {
        HelpHeading = heading;
        HelpContent = content;
        Changed?.Invoke();
    }

    public void SetStats(RenderFragment? content)
    {
        StatsContent = content;
        Changed?.Invoke();
    }

    public void SetActions(RenderFragment? content)
    {
        ActionsContent = content;
        Changed?.Invoke();
    }

    public async Task SetHelpVisibleAsync(bool visible)
    {
        HelpVisible = visible;
        await js.InvokeVoidAsync("CanDoItAll.browserState.saveCookie", HelpVisibleCookieName, visible ? "true" : "false");
        Changed?.Invoke();
    }

    public async Task SetStatsVisibleAsync(bool visible)
    {
        StatsVisible = visible;
        await js.InvokeVoidAsync("CanDoItAll.browserState.saveCookie", StatsVisibleCookieName, visible ? "true" : "false");
        Changed?.Invoke();
    }
}
