using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CanDoItAll.AppComponents;

/// <summary>
/// Per-circuit channel a routed page uses to register its tab-breadcrumb text, trailing action
/// buttons, help content, and stats content with the shell's single <c>AppToolbar</c> instance,
/// instead of each page declaring its own toolbar/help/stats markup. Pages register via
/// <c>AppToolbarActions</c>/<c>AppToolbarHelp</c>/<c>AppToolbarStats</c> (which clear their own
/// registration on dispose) and register the plain-string breadcrumb tab segment via
/// <c>AppToolbarTabText</c>, which likewise clears its own registration on dispose.
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
/// <see cref="TabText"/> tracks which component instance last set it (<see cref="SetTabText"/> takes an
/// owner token) so that a late <see cref="ClearTabText"/> from a disposing component that has already
/// been superseded by a newer one is a no-op instead of wiping the newer value — Blazor does not guarantee
/// the old page's <c>Dispose()</c> runs before the new page's <c>OnParametersSet</c> during navigation.
/// </summary>
public sealed class AppToolbarState(IJSRuntime js)
{
    public const string HelpVisibleCookieName = "candoitall.app-toolbar.help-visible";
    public const string StatsVisibleCookieName = "candoitall.app-toolbar.stats-visible";

    public string? TabText { get; private set; }

    private object? tabTextOwner;

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

    /// <summary>
    /// Registers <paramref name="tabText"/> as the trailing breadcrumb segment on behalf of
    /// <paramref name="owner"/>. The owner is remembered so a subsequent <see cref="ClearTabText"/> from a
    /// different, already-superseded owner does not wipe this value.
    /// </summary>
    public void SetTabText(object owner, string? tabText)
    {
        tabTextOwner = owner;

        if (string.Equals(TabText, tabText, StringComparison.Ordinal))
        {
            return;
        }

        TabText = tabText;
        Changed?.Invoke();
    }

    /// <summary>
    /// Clears the breadcrumb tab text registered by <paramref name="owner"/>, but only if
    /// <paramref name="owner"/> is still the current owner — a no-op otherwise, so a component that
    /// disposes after a newer page has already registered its own tab text cannot clobber it.
    /// </summary>
    public void ClearTabText(object owner)
    {
        if (!ReferenceEquals(tabTextOwner, owner))
        {
            return;
        }

        tabTextOwner = null;

        if (TabText is null)
        {
            return;
        }

        TabText = null;
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
