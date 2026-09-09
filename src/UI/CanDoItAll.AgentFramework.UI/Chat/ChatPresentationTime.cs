using System.Globalization;

namespace CanDoItAll.AgentFramework.UI.Chat;

public static class ChatPresentationTime {
    public static string Format(DateTimeOffset value)
        => value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
}
