namespace CanDoItAll.Web;

public sealed class WebHostRuntimeOptions
{
    public const string SectionName = "WebHost";

    public bool HttpsRedirectionEnabled { get; set; } = true;
}
