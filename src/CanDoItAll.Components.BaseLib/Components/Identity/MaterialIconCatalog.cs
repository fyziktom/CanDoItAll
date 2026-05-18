namespace CanDoItAll.Components.BaseLib;

public static class MaterialIconCatalog
{
    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ai"] = "smart_toy",
        ["arch"] = "design_services",
        ["arrow_selector_tool"] = "near_me",
        ["asset"] = "folder_open",
        ["assure"] = "fact_check",
        ["audio"] = "audiotrack",
        ["audit"] = "fact_check",
        ["backlog"] = "inbox",
        ["bars_staggered"] = "view_headline",
        ["block"] = "view_in_ar",
        ["calendar"] = "calendar_month",
        ["choice"] = "gavel",
        ["circle_info"] = "info",
        ["cloud_sync"] = "sync",
        ["console"] = "terminal",
        ["database"] = "storage",
        ["date"] = "calendar_month",
        ["deliver"] = "local_shipping",
        ["docx"] = "description",
        ["docker"] = "developer_board",
        ["domain"] = "public",
        ["dotnet"] = "code",
        ["evidence"] = "photo_camera",
        ["excel"] = "table_chart",
        ["expand"] = "open_in_full",
        ["feature"] = "star",
        ["file"] = "insert_drive_file",
        ["flow"] = "account_tree",
        ["fork"] = "call_split",
        ["frame"] = "crop_square",
        ["freelancer"] = "person",
        ["infra"] = "dns",
        ["issue"] = "bug_report",
        ["json"] = "code",
        ["location"] = "location_on",
        ["log"] = "receipt_long",
        ["markdown"] = "article",
        ["market"] = "campaign",
        ["meeting"] = "event_available",
        ["mermaid"] = "account_tree",
        ["migration"] = "shuffle",
        ["money"] = "payments",
        ["more_up"] = "expand_less",
        ["note"] = "note",
        ["open"] = "open_in_new",
        ["ops"] = "settings",
        ["org"] = "account_tree",
        ["partner"] = "handshake",
        ["payment"] = "payments",
        ["pdf"] = "picture_as_pdf",
        ["phase"] = "layers",
        ["plan"] = "calendar_month",
        ["plug"] = "power",
        ["powershell"] = "terminal",
        ["progress"] = "trending_up",
        ["progress-0"] = "radio_button_unchecked",
        ["progress-100"] = "check_circle",
        ["progress-na"] = "remove_circle_outline",
        ["progress-started"] = "play_arrow",
        ["prompt"] = "auto_awesome",
        ["qa"] = "fact_check",
        ["recording"] = "mic",
        ["release"] = "rocket_launch",
        ["relink"] = "sync_alt",
        ["repo"] = "account_tree",
        ["research"] = "manage_search",
        ["rev"] = "history",
        ["revision"] = "history",
        ["risk"] = "warning",
        ["runtime"] = "terminal",
        ["section"] = "grid_view",
        ["server"] = "dns",
        ["session"] = "web_asset",
        ["ship"] = "local_shipping",
        ["skip"] = "skip_next",
        ["step"] = "format_list_numbered",
        ["summary"] = "pie_chart",
        ["support"] = "support_agent",
        ["tailwind"] = "air",
        ["task"] = "task",
        ["test"] = "science",
        ["text"] = "text_snippet",
        ["transcript"] = "closed_caption",
        ["unlink"] = "link_off",
        ["use"] = "done_all",
        ["video"] = "videocam",
        ["watch"] = "visibility",
        ["marker"] = "bookmark",
        ["marker-alert"] = "warning",
        ["marker-car"] = "directions_car",
        ["marker-idea"] = "lightbulb",
        ["marker-money"] = "payments",
        ["marker-none"] = "bookmark_border",
        ["marker-pause"] = "pause_circle",
        ["marker-question"] = "help",
        ["marker-risk"] = "report_problem",
        ["marker-stop"] = "stop_circle",
        ["marker-thumbs-down"] = "thumb_down",
        ["marker-thumbs-up"] = "thumb_up",
        ["priority"] = "priority_high",
        ["priority-0"] = "remove_circle_outline",
        ["priority-1"] = "looks_one",
        ["priority-2"] = "looks_two",
        ["priority-3"] = "looks_3",
        ["priority-4"] = "looks_4",
        ["priority-5"] = "looks_5",
        ["priority-6"] = "looks_6",
        ["fa-angle-right"] = "chevron_right"
    };

    public static bool TryResolveIconName(string? iconToken, out string iconName)
    {
        iconName = string.Empty;
        if (string.IsNullOrWhiteSpace(iconToken))
        {
            return false;
        }

        var normalizedToken = iconToken.Trim();
        if (Aliases.TryGetValue(normalizedToken, out var mappedIconName))
        {
            iconName = mappedIconName;
            return true;
        }

        if (normalizedToken.Contains("fa-", StringComparison.OrdinalIgnoreCase)
            && TryResolveFontAwesomeLiteral(normalizedToken, out iconName))
        {
            return true;
        }

        if (LooksLikeMaterialIconName(normalizedToken))
        {
            iconName = normalizedToken;
            return true;
        }

        return false;
    }

    private static bool TryResolveFontAwesomeLiteral(string iconToken, out string iconName)
    {
        if (iconToken.Contains("fa-angle-right", StringComparison.OrdinalIgnoreCase)
            || iconToken.Contains("fa-chevron-right", StringComparison.OrdinalIgnoreCase))
        {
            iconName = "chevron_right";
            return true;
        }

        if (iconToken.Contains("fa-chevron-down", StringComparison.OrdinalIgnoreCase))
        {
            iconName = "expand_more";
            return true;
        }

        if (iconToken.Contains("fa-chevron-up", StringComparison.OrdinalIgnoreCase))
        {
            iconName = "expand_less";
            return true;
        }

        if (iconToken.Contains("fa-xmark", StringComparison.OrdinalIgnoreCase)
            || iconToken.Contains("fa-times", StringComparison.OrdinalIgnoreCase))
        {
            iconName = "close";
            return true;
        }

        iconName = string.Empty;
        return false;
    }

    private static bool LooksLikeMaterialIconName(string iconToken)
    {
        foreach (var character in iconToken)
        {
            if (character is >= 'a' and <= 'z')
            {
                continue;
            }

            if (character is >= '0' and <= '9')
            {
                continue;
            }

            if (character == '_')
            {
                continue;
            }

            return false;
        }

        return iconToken.Length > 0;
    }
}
