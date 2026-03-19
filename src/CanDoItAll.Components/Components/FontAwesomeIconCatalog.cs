namespace CanDoItAll.Components;

internal static class FontAwesomeIconCatalog
{
    private static readonly IReadOnlyDictionary<string, string> MaterialToFontAwesome = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["account_balance_wallet"] = "fa-solid fa-wallet",
        ["account_circle"] = "fa-solid fa-circle-user",
        ["add"] = "fa-solid fa-plus",
        ["add_card"] = "fa-solid fa-credit-card",
        ["add_circle"] = "fa-solid fa-circle-plus",
        ["admin_panel_settings"] = "fa-solid fa-user-shield",
        ["album"] = "fa-solid fa-compact-disc",
        ["archive"] = "fa-solid fa-box-archive",
        ["arrow_back"] = "fa-solid fa-arrow-left",
        ["arrow_forward"] = "fa-solid fa-arrow-right",
        ["article"] = "fa-solid fa-file-lines",
        ["auto_awesome"] = "fa-solid fa-wand-magic-sparkles",
        ["bolt"] = "fa-solid fa-bolt",
        ["bookmark_add"] = "fa-solid fa-bookmark",
        ["cancel"] = "fa-solid fa-ban",
        ["center_focus_strong"] = "fa-solid fa-bullseye",
        ["check"] = "fa-solid fa-check",
        ["check_circle"] = "fa-solid fa-circle-check",
        ["cleaning_services"] = "fa-solid fa-broom",
        ["clear"] = "fa-solid fa-eraser",
        ["close"] = "fa-solid fa-xmark",
        ["cloud_download"] = "fa-solid fa-cloud-arrow-down",
        ["cloud_sync"] = "fa-solid fa-cloud-arrow-up",
        ["content_copy"] = "fa-solid fa-copy",
        ["crop_square"] = "fa-regular fa-square",
        ["dashboard"] = "fa-solid fa-gauge-high",
        ["delete"] = "fa-solid fa-trash",
        ["delete_forever"] = "fa-solid fa-trash-can",
        ["delete_sweep"] = "fa-solid fa-broom",
        ["description"] = "fa-solid fa-file-lines",
        ["developer_mode"] = "fa-solid fa-code",
        ["download"] = "fa-solid fa-download",
        ["draw"] = "fa-solid fa-pen",
        ["expand_less"] = "fa-solid fa-chevron-up",
        ["expand_more"] = "fa-solid fa-chevron-down",
        ["favorite"] = "fa-solid fa-heart",
        ["first_page"] = "fa-solid fa-backward-fast",
        ["gavel"] = "fa-solid fa-gavel",
        ["graphic_eq"] = "fa-solid fa-sliders",
        ["groups"] = "fa-solid fa-users",
        ["hearing"] = "fa-solid fa-headphones",
        ["hearing_disabled"] = "fa-solid fa-volume-xmark",
        ["help"] = "fa-solid fa-circle-question",
        ["help_outline"] = "fa-regular fa-circle-question",
        ["home"] = "fa-solid fa-house",
        ["image"] = "fa-solid fa-image",
        ["info"] = "fa-solid fa-circle-info",
        ["inventory_2"] = "fa-solid fa-box-open",
        ["library_music"] = "fa-solid fa-book-open",
        ["link_off"] = "fa-solid fa-link-slash",
        ["login"] = "fa-solid fa-right-to-bracket",
        ["logout"] = "fa-solid fa-right-from-bracket",
        ["looks_3"] = "fa-solid fa-list-ol",
        ["looks_one"] = "fa-solid fa-list-ol",
        ["looks_two"] = "fa-solid fa-list-ol",
        ["manage_accounts"] = "fa-solid fa-users-gear",
        ["menu"] = "fa-solid fa-bars",
        ["monitor_heart"] = "fa-solid fa-heart-pulse",
        ["music_note"] = "fa-solid fa-music",
        ["network_ping"] = "fa-solid fa-signal",
        ["note_add"] = "fa-solid fa-note-sticky",
        ["open_in_new"] = "fa-solid fa-up-right-from-square",
        ["paid"] = "fa-solid fa-money-check-dollar",
        ["pause"] = "fa-solid fa-pause",
        ["payments"] = "fa-solid fa-money-bill-wave",
        ["person_add"] = "fa-solid fa-user-plus",
        ["piano"] = "fa-solid fa-keyboard",
        ["picture_as_pdf"] = "fa-solid fa-file-pdf",
        ["play_arrow"] = "fa-solid fa-play",
        ["play_circle"] = "fa-solid fa-circle-play",
        ["playlist_add_check"] = "fa-solid fa-list-check",
        ["playlist_play"] = "fa-solid fa-play",
        ["playlist_remove"] = "fa-solid fa-list",
        ["power"] = "fa-solid fa-power-off",
        ["publish"] = "fa-solid fa-upload",
        ["published_with_changes"] = "fa-solid fa-list-check",
        ["query_stats"] = "fa-solid fa-chart-line",
        ["radar"] = "fa-solid fa-satellite-dish",
        ["refresh"] = "fa-solid fa-rotate",
        ["remove"] = "fa-solid fa-minus",
        ["replay"] = "fa-solid fa-rotate-left",
        ["restart_alt"] = "fa-solid fa-rotate-left",
        ["restore"] = "fa-solid fa-rotate-left",
        ["rocket_launch"] = "fa-solid fa-rocket",
        ["save"] = "fa-solid fa-floppy-disk",
        ["school"] = "fa-solid fa-graduation-cap",
        ["search"] = "fa-solid fa-magnifying-glass",
        ["sell"] = "fa-solid fa-tags",
        ["sensors"] = "fa-solid fa-satellite-dish",
        ["settings"] = "fa-solid fa-gear",
        ["shopping_cart"] = "fa-solid fa-cart-shopping",
        ["skip_next"] = "fa-solid fa-forward-step",
        ["skip_previous"] = "fa-solid fa-backward-step",
        ["speed"] = "fa-solid fa-gauge",
        ["star"] = "fa-solid fa-star",
        ["stop"] = "fa-solid fa-stop",
        ["store"] = "fa-solid fa-store",
        ["subdirectory_arrow_left"] = "fa-solid fa-reply",
        ["sync"] = "fa-solid fa-arrows-rotate",
        ["task_alt"] = "fa-solid fa-circle-check",
        ["token"] = "fa-solid fa-coins",
        ["touch_app"] = "fa-solid fa-hand-pointer",
        ["track_changes"] = "fa-solid fa-bullseye",
        ["tune"] = "fa-solid fa-sliders",
        ["unfold_more"] = "fa-solid fa-up-down",
        ["upload"] = "fa-solid fa-upload",
        ["upload_file"] = "fa-solid fa-file-arrow-up",
        ["usb"] = "fa-brands fa-usb",
        ["visibility"] = "fa-solid fa-eye",
        ["volume_up"] = "fa-solid fa-volume-high",
        ["workspace_premium"] = "fa-solid fa-crown"
    };

    public static bool TryResolveCssClass(string? iconToken, out string cssClass)
    {
        cssClass = string.Empty;
        if (string.IsNullOrWhiteSpace(iconToken))
        {
            return false;
        }

        var normalizedToken = iconToken.Trim();
        if (IsFontAwesomeClassList(normalizedToken))
        {
            cssClass = normalizedToken;
            return true;
        }

        if (MaterialToFontAwesome.TryGetValue(normalizedToken, out var mappedCssClass))
        {
            cssClass = mappedCssClass;
            return true;
        }

        return false;
    }

    private static bool IsFontAwesomeClassList(string iconToken)
    {
        return iconToken.Contains("fa-", StringComparison.OrdinalIgnoreCase);
    }
}
