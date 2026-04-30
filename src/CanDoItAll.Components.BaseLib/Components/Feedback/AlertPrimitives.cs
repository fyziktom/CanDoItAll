using System.Collections.ObjectModel;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Components.BaseLib;

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

public enum NotificationSeverity
{
    Info,
    Success,
    Warning,
    Error
}

public enum NotificationPosition
{
    TopRight = 0,
    TopLeft = 1,
    TopCenter = 2,
    BottomRight = 3,
    BottomLeft = 4,
    BottomCenter = 5,
    CenterLeft = 6,
    CenterRight = 7,
    Center = 8,
    LeftTop = TopLeft,
    LeftCenter = CenterLeft,
    LeftBottom = BottomLeft,
    RightTop = TopRight,
    RightCenter = CenterRight,
    RightBottom = BottomRight
}

public static class NotificationDurations
{
    public const double ConfirmationMilliseconds = 3000;
    public const double InformationMilliseconds = 5000;
    public const double WarningMilliseconds = 9000;
    public const double ErrorMilliseconds = 14000;
}

public enum CalloutTone
{
    Default,
    Ok
}

public sealed class NotificationMessage : IEquatable<NotificationMessage>
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    public string Summary { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;

    public double? Duration { get; set; } = 3000;

    public string? Class { get; set; }

    public string? Style { get; set; }

    public string? TestId { get; set; }

    public NotificationPosition? Position { get; set; }

    public bool CloseOnClick { get; set; }

    public bool ShowProgress { get; set; }

    public object? Payload { get; set; }

    public Action<NotificationMessage>? Click { get; set; }

    public Action<NotificationMessage>? Close { get; set; }

    public RenderFragment<NotificationService>? SummaryContent { get; set; }

    public RenderFragment<NotificationService>? DetailContent { get; set; }

    public string CloseAriaLabel { get; set; } = "Dismiss notification";

    public bool IsPersistent => Duration is null or <= 0;

    public bool Equals(NotificationMessage? other)
    {
        return other is not null && Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as NotificationMessage);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}

public sealed class NotificationService
{
    public ObservableCollection<NotificationMessage> Messages { get; } = [];

    public event Action<NotificationMessage>? Notification;

    public event Action? Changed;

    public void Notify(NotificationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!Messages.Contains(message))
        {
            Messages.Add(message);
        }

        Notification?.Invoke(message);
        Changed?.Invoke();
    }

    public NotificationMessage Notify(
        NotificationSeverity severity = NotificationSeverity.Info,
        string summary = "",
        string detail = "",
        double? duration = null,
        Action<NotificationMessage>? click = null,
        bool closeOnClick = false,
        object? payload = null,
        Action<NotificationMessage>? close = null,
        NotificationPosition? position = null)
    {
        var message = new NotificationMessage
        {
            Severity = severity,
            Summary = summary,
            Detail = detail,
            Duration = duration ?? ResolveDefaultDuration(severity),
            Click = click,
            Close = close,
            CloseOnClick = closeOnClick,
            Payload = payload,
            Position = position
        };

        Notify(message);
        return message;
    }

    public NotificationMessage Notify(
        NotificationSeverity severity,
        string summary,
        string detail,
        TimeSpan duration,
        Action<NotificationMessage>? click = null,
        NotificationPosition? position = null)
    {
        return Notify(severity, summary, detail, duration.TotalMilliseconds, click, position: position);
    }

    public NotificationMessage Success(
        string summary,
        string detail = "",
        double? duration = null,
        NotificationPosition? position = null)
    {
        return Notify(NotificationSeverity.Success, summary, detail, duration, position: position);
    }

    public NotificationMessage Info(
        string summary,
        string detail = "",
        double? duration = null,
        NotificationPosition? position = null)
    {
        return Notify(NotificationSeverity.Info, summary, detail, duration, position: position);
    }

    public NotificationMessage Warning(
        string summary,
        string detail = "",
        double? duration = null,
        NotificationPosition? position = null)
    {
        return Notify(NotificationSeverity.Warning, summary, detail, duration, position: position);
    }

    public NotificationMessage Error(
        string summary,
        string detail = "",
        double? duration = null,
        NotificationPosition? position = null)
    {
        return Notify(NotificationSeverity.Error, summary, detail, duration, position: position);
    }

    public bool Dismiss(NotificationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!Messages.Remove(message))
        {
            return false;
        }

        message.Close?.Invoke(message);
        Changed?.Invoke();
        return true;
    }

    public bool Dismiss(Guid id)
    {
        var message = Messages.FirstOrDefault(candidate => candidate.Id == id);
        return message is not null && Dismiss(message);
    }

    public void Clear()
    {
        foreach (var message in Messages.ToArray())
        {
            Dismiss(message);
        }
    }

    public void Click(NotificationMessage message)
    {
        message.Click?.Invoke(message);
        if (message.CloseOnClick)
        {
            Dismiss(message);
        }
    }

    private static double ResolveDefaultDuration(NotificationSeverity severity)
    {
        return severity switch
        {
            NotificationSeverity.Success => NotificationDurations.ConfirmationMilliseconds,
            NotificationSeverity.Warning => NotificationDurations.WarningMilliseconds,
            NotificationSeverity.Error => NotificationDurations.ErrorMilliseconds,
            _ => NotificationDurations.InformationMilliseconds
        };
    }
}
