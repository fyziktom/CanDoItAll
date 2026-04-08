using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureActionShortcuts
{
    private const string FallbackShortcutAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    private static readonly IReadOnlyDictionary<string, string> FixedShortcuts =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["add-note"] = "n",
            ["group-blocks"] = "b",
            ["group-assets"] = "a",
            ["group-people"] = "p",
            ["group-infrastructure"] = "i",
            ["group-work"] = "w",
            ["group-meetings"] = "q",
            ["marker"] = "m",
            ["add-block-delivery"] = "d",
            ["add-block-backlog"] = "b",
            ["add-block-support"] = "s",
            ["add-block-feature"] = "f",
            ["add-file-pdf"] = "p",
            ["add-file-excel"] = "e",
            ["add-file-docx"] = "w",
            ["add-file-json"] = "j",
            ["add-file-text"] = "t",
            ["marker:question"] = "q",
            ["marker:alert"] = "e",
            ["add-meeting-onsite"] = "s",
            ["add-meeting-online"] = "o",
            ["add-work-task"] = "t",
            ["progress:0"] = "0",
            ["progress:started"] = "s",
            ["progress:10"] = "1",
            ["progress:20"] = "2",
            ["progress:30"] = "3",
            ["progress:40"] = "4",
            ["progress:50"] = "5",
            ["progress:60"] = "6",
            ["progress:70"] = "7",
            ["progress:80"] = "8",
            ["progress:90"] = "9",
            ["progress:100"] = "c",
            ["progress:na"] = "n",
            ["priority:0"] = "0",
            ["priority:1"] = "1",
            ["priority:2"] = "2",
            ["priority:3"] = "3",
            ["priority:4"] = "4",
            ["priority:5"] = "5",
            ["priority:6"] = "6"
        };

    public static IReadOnlyList<CanvasWorkbenchAction> Apply(IReadOnlyList<CanvasWorkbenchAction> actions)
    {
        AssignLayer(actions);
        return actions;
    }

    private static void AssignLayer(IReadOnlyList<CanvasWorkbenchAction> actions)
    {
        if (actions.Count == 0)
        {
            return;
        }

        var reservedShortcuts = new Dictionary<CanvasWorkbenchAction, string>();
        var usedShortcuts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var action in actions)
        {
            action.ShortcutKey = string.Empty;
            if (!FixedShortcuts.TryGetValue(action.ActionId, out var fixedShortcut))
            {
                continue;
            }

            var normalizedShortcut = NormalizeShortcut(fixedShortcut);
            if (!usedShortcuts.Add(normalizedShortcut))
            {
                throw new InvalidOperationException(
                    $"Duplicate fixed shortcut '{normalizedShortcut}' for sibling layer action '{action.ActionId}'.");
            }

            reservedShortcuts[action] = normalizedShortcut;
            action.ShortcutKey = normalizedShortcut;
        }

        foreach (var action in actions)
        {
            if (reservedShortcuts.ContainsKey(action))
            {
                continue;
            }

            var resolvedShortcut = EnumerateCandidates(action)
                .FirstOrDefault(candidate => !usedShortcuts.Contains(candidate));

            if (string.IsNullOrWhiteSpace(resolvedShortcut))
            {
                throw new InvalidOperationException(
                    $"Unable to resolve a shortcut for action '{action.ActionId}' in the current sibling layer.");
            }

            usedShortcuts.Add(resolvedShortcut);
            action.ShortcutKey = resolvedShortcut;
        }

        foreach (var action in actions)
        {
            AssignLayer(action.Children);
        }
    }

    private static IEnumerable<string> EnumerateCandidates(CanvasWorkbenchAction action)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in EnumerateCandidateSources(action))
        {
            foreach (var candidate in EnumerateWordInitials(source))
            {
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }

            foreach (var candidate in EnumerateCharacters(source))
            {
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        foreach (var fallbackCharacter in FallbackShortcutAlphabet)
        {
            var fallback = fallbackCharacter.ToString();
            if (seen.Add(fallback))
            {
                yield return fallback;
            }
        }
    }

    private static IEnumerable<string> EnumerateCandidateSources(CanvasWorkbenchAction action)
    {
        if (!string.IsNullOrWhiteSpace(action.MenuLabel))
        {
            yield return action.MenuLabel;
        }

        if (!string.IsNullOrWhiteSpace(action.Label) &&
            !string.Equals(action.Label, action.MenuLabel, StringComparison.OrdinalIgnoreCase))
        {
            yield return action.Label;
        }

        if (!string.IsNullOrWhiteSpace(action.ActionId))
        {
            yield return action.ActionId.Replace('-', ' ').Replace(':', ' ');
        }
    }

    private static IEnumerable<string> EnumerateWordInitials(string text)
    {
        var isWordStart = true;
        foreach (var character in text)
        {
            if (!IsShortcutCandidate(character))
            {
                isWordStart = true;
                continue;
            }

            if (isWordStart)
            {
                yield return NormalizeShortcut(character.ToString());
            }

            isWordStart = false;
        }
    }

    private static IEnumerable<string> EnumerateCharacters(string text)
    {
        foreach (var character in text)
        {
            if (!IsShortcutCandidate(character))
            {
                continue;
            }

            yield return NormalizeShortcut(character.ToString());
        }
    }

    private static bool IsShortcutCandidate(char character)
        => character <= sbyte.MaxValue && char.IsLetterOrDigit(character);

    private static string NormalizeShortcut(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
        {
            throw new InvalidOperationException("Shortcut values must not be empty.");
        }

        var trimmed = shortcut.Trim();
        if (trimmed.Length != 1 || !IsShortcutCandidate(trimmed[0]))
        {
            throw new InvalidOperationException(
                $"Shortcut '{shortcut}' is invalid. Shortcuts must be a single ASCII letter or digit.");
        }

        return char.ToLowerInvariant(trimmed[0]).ToString();
    }
}
