using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Runtime;

using static CanDoItAll.Modules.Processes.ProcessRuntimeOwnedToolReceiptFactory;

namespace CanDoItAll.Modules.Processes;

internal sealed record DotNetQualityRepairScaffoldInputs(
    string ProductRootAlias,
    string AppProjectDirectory,
    string SolutionFileAlias,
    string TestProjectFileAlias,
    string ScriptRef,
    string Script,
    string SideEffectManifest)
{
    internal static bool TryResolve(
        ProcessRuntimeStepAssignment assignment,
        out DotNetQualityRepairScaffoldInputs inputs)
    {
        inputs = null!;
        if (!TryGet(assignment.LaunchVariables, "ProductRootAlias", out var productRootAlias) ||
            !TryGet(assignment.LaunchVariables, "DotNetAppProjectDirectory", out var appProjectDirectory) ||
            !TryGet(assignment.LaunchVariables, "DotNetSolutionFileAlias", out var solutionFileAlias) ||
            !TryGet(assignment.LaunchVariables, "DotNetTestProjectFileAlias", out var testProjectFileAlias) ||
            !TryGet(assignment.LaunchVariables, "DotNetScaffoldRepairScriptRef", out var scriptRef) ||
            !TryGet(assignment.LaunchVariables, "DotNetScaffoldRepairScript", out var script) ||
            !TryGet(assignment.LaunchVariables, "DotNetScaffoldRepairSideEffectManifest", out var sideEffectManifest))
        {
            return false;
        }

        inputs = new DotNetQualityRepairScaffoldInputs(
            productRootAlias,
            Path.GetFullPath(appProjectDirectory),
            solutionFileAlias,
            testProjectFileAlias,
            scriptRef.Replace("{CurrentProcessRunId}", assignment.RunId.Value.ToString("D"), StringComparison.OrdinalIgnoreCase),
            script,
            sideEffectManifest);
        return true;
    }

    private static bool TryGet(
        IReadOnlyDictionary<string, string> variables,
        string key,
        out string value)
    {
        value = string.Empty;
        if (!variables.TryGetValue(key, out var candidate) || string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        value = candidate.Trim();
        return true;
    }
}

internal sealed record DotNetScaffoldState(
    bool HasAspNetHelpLink,
    bool HasStockNavigation,
    bool HasStockCounter,
    bool HasStockWeather,
    bool IsBlazorErrorUiRuleMissing)
{
    internal bool HasStockScaffoldResidue =>
        HasAspNetHelpLink || HasStockNavigation || HasStockCounter || HasStockWeather || IsBlazorErrorUiRuleMissing;

    internal IEnumerable<string> DescribeResidue()
    {
        if (HasAspNetHelpLink)
        {
            yield return "stock ASP.NET Core help link";
        }

        if (HasStockNavigation)
        {
            yield return "stock Counter/Weather navigation";
        }

        if (HasStockCounter)
        {
            yield return "fingerprint-matched stock or generated-stub Counter page";
        }

        if (HasStockWeather)
        {
            yield return "fingerprint-matched stock or generated-stub Weather page";
        }

        if (IsBlazorErrorUiRuleMissing)
        {
            yield return "missing hidden #blazor-error-ui rule";
        }
    }
}

internal sealed class DotNetScaffoldResidueInspector(IWorkspaceFileService workspaceFiles)
{
    private const int MaximumSourceCharacters = 200000;

    internal DotNetScaffoldState Read(
        DotNetQualityRepairScaffoldInputs inputs,
        Guid executionRunId,
        ICollection<ToolExecutionReceiptRecord> receipts)
    {
        var mainLayout = ReadSource(inputs, "Layout/MainLayout.razor", executionRunId, receipts);
        var navMenu = ReadSource(inputs, "Layout/NavMenu.razor", executionRunId, receipts);
        var counter = ReadSource(inputs, "Pages/Counter.razor", executionRunId, receipts);
        var weather = ReadSource(inputs, "Pages/Weather.razor", executionRunId, receipts);
        var appCss = ReadSource(inputs, "wwwroot/css/app.css", executionRunId, receipts);
        return new DotNetScaffoldState(
            mainLayout.Contains("learn.microsoft.com/aspnet/core/", StringComparison.OrdinalIgnoreCase),
            navMenu.Contains("href=\"counter\"", StringComparison.OrdinalIgnoreCase) &&
            navMenu.Contains("href=\"weather\"", StringComparison.OrdinalIgnoreCase),
            (counter.Contains("currentCount", StringComparison.OrdinalIgnoreCase) &&
             counter.Contains("Click me", StringComparison.OrdinalIgnoreCase)) ||
            IsGeneratedStarterPlaceholder(counter, "/counter"),
            (weather.Contains("WeatherForecast", StringComparison.OrdinalIgnoreCase) &&
             weather.Contains("sample-data/weather.json", StringComparison.OrdinalIgnoreCase)) ||
            IsGeneratedStarterPlaceholder(weather, "/weather"),
            !appCss.Contains("#blazor-error-ui", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsGeneratedStarterPlaceholder(string content, string route)
    {
        if (content.Length == 0 ||
            content.Length > 800 ||
            !content.Contains($"@page \"{route}\"", StringComparison.OrdinalIgnoreCase) ||
            ContainsFunctionalRazorSurface(content))
        {
            return false;
        }

        return ContainsAny(
            content,
            "starter",
            "sample",
            "scaffold",
            "redirect to",
            "removed from",
            "without product content");
    }

    private static bool ContainsFunctionalRazorSurface(string content)
        => ContainsAny(
            content,
            "@code",
            "@inject",
            "@onclick",
            "<button",
            "<EditForm",
            "NavigationManager",
            "<svg");

    private static bool ContainsAny(string content, params string[] values)
        => values.Any(value => content.Contains(value, StringComparison.OrdinalIgnoreCase));

    private string ReadSource(
        DotNetQualityRepairScaffoldInputs inputs,
        string relativePath,
        Guid executionRunId,
        ICollection<ToolExecutionReceiptRecord> receipts)
    {
        var path = $"{ToExternalTargetAlias(inputs.AppProjectDirectory).TrimEnd('/')}/{relativePath}";
        var result = workspaceFiles.ReadTextFile(path, MaximumSourceCharacters);
        receipts.Add(From(executionRunId, result));
        return result.Succeeded && !result.IsTruncated ? result.Content : string.Empty;
    }

    private static string ToExternalTargetAlias(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var rootPath = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return fullPath;
        }

        var trimmedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmedRoot.Length != 2 || trimmedRoot[1] != ':' || !char.IsLetter(trimmedRoot[0]))
        {
            return fullPath;
        }

        var relativePath = fullPath[rootPath.Length..]
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
        return string.IsNullOrWhiteSpace(relativePath)
            ? $"external-target/{char.ToUpperInvariant(trimmedRoot[0])}"
            : $"external-target/{char.ToUpperInvariant(trimmedRoot[0])}/{relativePath.Replace(Path.DirectorySeparatorChar, '/')}";
    }
}
