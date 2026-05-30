using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.AppComponents;

public enum Orientation
{
    Horizontal,
    Vertical
}

public enum AlignItems
{
    Start,
    Center,
    End,
    Stretch
}

public enum JustifyContent
{
    Start,
    Center,
    End,
    SpaceBetween,
    SpaceAround
}

public enum FlexWrap
{
    NoWrap,
    Wrap
}

public enum ButtonStyle
{
    Primary,
    Secondary,
    Success,
    Info,
    Warning,
    Danger,
    Light,
    Dark,
    Base
}

public enum ButtonSize
{
    Small,
    Medium,
    Large
}

public enum Variant
{
    Filled,
    Flat,
    Outlined,
    Text
}

public enum Shade
{
    Default,
    Lighter,
    Light,
    Dark,
    Darker
}

public enum TextStyle
{
    Body1,
    Body2,
    Caption,
    H4,
    H5,
    H6,
    Subtitle1,
    Subtitle2
}

public enum AlertStyle
{
    Base,
    Primary,
    Secondary,
    Success,
    Info,
    Warning,
    Danger,
    Light,
    Dark
}

public enum TabRenderMode
{
    Server,
    Client
}

public enum NotificationSeverity
{
    Info,
    Success,
    Warning,
    Error
}

public sealed class NotificationMessage
{
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    public string? Summary { get; set; }

    public string? Detail { get; set; }

    public double Duration { get; set; } = 2500;
}

public sealed class NotificationService
{
    public event Action<NotificationMessage>? Notification;

    public void Notify(NotificationMessage message)
    {
        Notification?.Invoke(message);
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCanDoItAllComponents(this IServiceCollection services)
    {
        services.AddScoped<NotificationService>();
        return services;
    }
}


public static class ComponentAttributeExtensions
{
    public static IReadOnlyDictionary<string, object>? WithClass(this IReadOnlyDictionary<string, object>? attributes, string? baseClass)
    {
        return WithClassAndStyle(attributes, baseClass, null);
    }

    public static IReadOnlyDictionary<string, object>? WithClassAndStyle(this IReadOnlyDictionary<string, object>? attributes, string? baseClass, string? baseStyle)
    {
        var merged = attributes is null
            ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object>(attributes, StringComparer.OrdinalIgnoreCase);

        var classFromAttributes = ReadAttribute(attributes, "class");
        var styleFromAttributes = ReadAttribute(attributes, "style");

        var classValue = JoinCssFragments(baseClass, classFromAttributes);
        var styleValue = JoinStyleFragments(baseStyle, styleFromAttributes);

        if (string.IsNullOrWhiteSpace(classValue))
        {
            merged.Remove("class");
        }
        else
        {
            merged["class"] = classValue;
        }

        if (string.IsNullOrWhiteSpace(styleValue))
        {
            merged.Remove("style");
        }
        else
        {
            merged["style"] = styleValue;
        }

        return merged.Count == 0
            ? null
            : merged;
    }

    private static string? ReadAttribute(IReadOnlyDictionary<string, object>? attributes, string key)
    {
        if (attributes is null)
        {
            return null;
        }

        foreach (var entry in attributes)
        {
            if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value?.ToString();
            }
        }

        return null;
    }

    private static string JoinCssFragments(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return second?.Trim() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(second))
        {
            return first.Trim();
        }

        return $"{first.Trim()} {second.Trim()}";
    }

    private static string JoinStyleFragments(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return second?.Trim() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(second))
        {
            return first.Trim();
        }

        return $"{TrimTrailingSemicolon(first)}; {second.Trim()}";
    }

    private static string TrimTrailingSemicolon(string value)
    {
        return value.Trim().TrimEnd(';');
    }
}
