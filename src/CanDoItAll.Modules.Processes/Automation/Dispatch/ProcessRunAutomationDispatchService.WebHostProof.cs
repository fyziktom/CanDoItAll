using System.Xml.Linq;

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

        if (TryResolveInvalidBlazorWebAssemblyStaticAssetSummary(projectFilePath, projectRoot, out summary))
        {
            return true;
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

    private static bool TryResolveInvalidBlazorWebAssemblyStaticAssetSummary(
        string projectFilePath,
        string projectRoot,
        out string summary)
    {
        summary = string.Empty;
        XDocument document;
        try
        {
            document = XDocument.Load(projectFilePath, LoadOptions.None);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        var sdk = document.Root?.Attribute("Sdk")?.Value ?? string.Empty;
        if (!sdk.Contains("Microsoft.NET.Sdk.BlazorWebAssembly", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IsNet8BlazorWebAssemblyProject(document))
        {
            return false;
        }

        var signals = new List<string>();
        if (document
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "OverrideHtmlAssetPlaceholders", StringComparison.OrdinalIgnoreCase))
            .Select(element => element.Value.Trim())
            .Any(value => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)))
        {
            signals.Add("OverrideHtmlAssetPlaceholders=true");
        }

        var indexPath = Path.Combine(projectRoot, "wwwroot", "index.html");
        if (File.Exists(indexPath))
        {
            string indexText;
            try
            {
                indexText = File.ReadAllText(indexPath);
            }
            catch (IOException)
            {
                indexText = string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                indexText = string.Empty;
            }

            AddTextSignal(indexText, "#[.{fingerprint}]", signals);
            if (indexText.Contains("<script type=\"importmap\"></script>", StringComparison.OrdinalIgnoreCase))
            {
                signals.Add("empty import map");
            }
        }

        if (signals.Count == 0)
        {
            return false;
        }

        var displayPath = TryMapAbsolutePathToExternalTargetAlias(projectFilePath);
        summary = $"detected Blazor WebAssembly static asset placeholder mismatch in {displayPath}: net8.0/ASP.NET Core 8 apps should not carry unresolved fingerprint placeholder mode without browser-proven 200 responses ({string.Join(", ", signals.Distinct(StringComparer.OrdinalIgnoreCase))}). Repair index.html/project settings to stable dev-server asset paths or prove the fingerprinted static assets are served before claiming runnable startup proof.";
        return true;
    }

    private static bool IsNet8BlazorWebAssemblyProject(XDocument document)
    {
        var targetFrameworks = document
            .Descendants()
            .Where(element =>
                string.Equals(element.Name.LocalName, "TargetFramework", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(element.Name.LocalName, "TargetFrameworks", StringComparison.OrdinalIgnoreCase))
            .SelectMany(element => element.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (targetFrameworks.Any(framework => framework.StartsWith("net8.0", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return document
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "PackageReference", StringComparison.OrdinalIgnoreCase))
            .Where(element => string.Equals(
                element.Attribute("Include")?.Value,
                "Microsoft.AspNetCore.Components.WebAssembly",
                StringComparison.OrdinalIgnoreCase))
            .Select(element => element.Attribute("Version")?.Value?.Trim() ?? string.Empty)
            .Any(version => version.StartsWith("8.", StringComparison.OrdinalIgnoreCase));
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
