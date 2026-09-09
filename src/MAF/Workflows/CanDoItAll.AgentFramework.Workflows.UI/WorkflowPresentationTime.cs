using System.Globalization;

namespace CanDoItAll.AgentFramework.Workflows.UI;

public static class WorkflowPresentationTime {
    public static string Format(DateTimeOffset value)
        => value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
}
