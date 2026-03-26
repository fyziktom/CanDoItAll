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

public enum CalloutTone
{
    Default,
    Ok
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
