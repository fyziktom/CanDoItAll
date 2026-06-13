namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static bool TryResolveInvalidWebHostShapeSummary(
        string projectFilePath,
        out string summary)
    {
        summary = string.Empty;
        var projectRoot = Path.GetDirectoryName(projectFilePath);
        if (string.IsNullOrWhiteSpace(projectRoot) || !Directory.Exists(projectRoot))
        {
            return false;
        }

        var hasCurrentBlazorWebAppFiles =
            File.Exists(Path.Combine(projectRoot, "Components", "App.razor")) &&
            File.Exists(Path.Combine(projectRoot, "Components", "Routes.razor"));
        if (!hasCurrentBlazorWebAppFiles)
        {
            return false;
        }

        var signals = new List<string>();
        AddExistingRelativeFileSignal(projectRoot, "Pages/_Host.cshtml", signals);
        AddExistingRelativeFileSignal(projectRoot, "Startup.cs", signals);

        var programPath = Path.Combine(projectRoot, "Program.cs");
        if (File.Exists(programPath))
        {
            string programText;
            try
            {
                programText = File.ReadAllText(programPath);
            }
            catch (IOException)
            {
                programText = string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                programText = string.Empty;
            }

            AddTextSignal(programText, "AddServerSideBlazor", signals);
            AddTextSignal(programText, "MapBlazorHub", signals);
            AddTextSignal(programText, "MapFallbackToPage", signals);
            AddTextSignal(programText, "UseStartup", signals);
        }

        if (signals.Count == 0)
        {
            return false;
        }

        var displayPath = TryMapAbsolutePathToExternalTargetAlias(projectFilePath);
        summary = $"detected mixed Blazor hosting shape in {displayPath}: current Blazor Web App files are present alongside legacy Blazor Server hosting artifacts or APIs ({string.Join(", ", signals.Distinct(StringComparer.OrdinalIgnoreCase))}). Repair the project to one hosting model before claiming runnable startup proof.";
        return true;
    }

    private static void AddExistingRelativeFileSignal(
        string root,
        string relativePath,
        List<string> signals)
    {
        var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath))
        {
            signals.Add(relativePath);
        }
    }

    private static void AddTextSignal(
        string text,
        string token,
        List<string> signals)
    {
        if (text.Contains(token, StringComparison.Ordinal))
        {
            signals.Add($"{token}(...)");
        }
    }
}
