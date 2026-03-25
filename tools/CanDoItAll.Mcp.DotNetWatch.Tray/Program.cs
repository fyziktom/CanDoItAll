using System.Runtime.Versioning;

namespace CanDoItAll.Mcp.DotNetWatch.Tray;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var options = TrayOptions.Parse(args);
        if (!string.IsNullOrWhiteSpace(options.HeadlessCommand))
        {
            Environment.Exit(TrayHeadlessRunner.RunAsync(options).GetAwaiter().GetResult());
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var context = new BackendTrayApplicationContext(options);
        Application.Run(context);
    }
}
